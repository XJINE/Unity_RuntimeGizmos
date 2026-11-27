using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using MethodDef = Mono.Cecil.MethodDefinition;
using TypeDef   = Mono.Cecil.TypeDefinition;
using MethodRef = Mono.Cecil.MethodReference;
using TypeRef   = Mono.Cecil.TypeReference;

namespace RuntimeGizmos.CodeGen {
internal sealed partial class RuntimeGizmosILPostProcessor : ILPostProcessor
{
    // NOTE:
    // Per MonoBehaviour-derived type whose OnDrawGizmos (own or inherited) uses RuntimeGizmos:
    // - If the type declares Update, "OnDrawGizmos()" is injected at its top,
    //   unless the body already calls OnDrawGizmos or a base Update that is woven.
    // - If the type declares no Update, one is generated. It calls the inherited
    //   base Update first (when one exists), then OnDrawGizmos.
    // - A private base Update in the same assembly is widened to protected so the
    //   generated Update can call it. An inaccessible base Update in another
    //   assembly causes a build error with workarounds in the message.
    // - A private RuntimeGizmos-using OnDrawGizmos is widened to protected so that
    //   woven Updates in derived classes (possibly in other assemblies) can call it.
    private sealed class Weaver
    {
        private readonly ModuleDefinition          _module;
        private readonly List<DiagnosticMessage>   _diagnostics;
        private readonly Dictionary<TypeDef, bool> _usesRuntimeGizmosByType = new ();

        private int _wovenCount;

        public Weaver(ModuleDefinition module, List<DiagnosticMessage> diagnostics)
        {
            _module      = module;
            _diagnostics = diagnostics;
        }

        public int Run()
        {
            foreach (var type in _module.GetAllTypes())
            {
                if (!IsProcessType(type))
                {
                    continue;
                }

                ProcessType(type);
            }

            return _wovenCount;
        }

        private static bool IsProcessType(TypeDef type)
        {
            if (type.IsInterface || type.IsValueType)
            {
                return false;
            }

            // Walk up the base types to see if this type derives from MonoBehaviour.

            var current = type.BaseType;

            while (current != null)
            {
                if (current.FullName == MonoBehaviourName)
                {
                    return true;
                }

                var def = SafeResolve(current);

                if (def == null)
                {
                    return false;
                }

                current = def.BaseType;
            }

            return false;
        }

        private void ProcessType(TypeDef type)
        {
            // Widen a private RuntimeGizmos-using OnDrawGizmos to protected,
            // so that woven Updates in derived classes in other assemblies can call it.

            var declaredOnDrawGizmos = GetDeclaredMethod(type, OnDrawGizmosName);

            if (declaredOnDrawGizmos != null
            &&  declaredOnDrawGizmos.IsPrivate
            &&  UsesRuntimeGizmos(declaredOnDrawGizmos))
            {
                declaredOnDrawGizmos.IsPrivate = false;
                declaredOnDrawGizmos.IsFamily  = true;

                _wovenCount++;
            }

            if (!UsesRuntimeGizmosInHierarchy(type))
            {
                return;
            }

            var updateMethod = GetDeclaredMethod(type, UpdateName);

            if (updateMethod != null)
            {
                if (!updateMethod.HasBody || HasOnDrawGizmosCall(updateMethod))
                {
                    return;
                }

                var (onDrawGizmos, isVirtual) = FindCallableOnDrawGizmos(type);

                if (onDrawGizmos == null)
                {
                    return;
                }

                // Insert "this.OnDrawGizmos()" at the top of the existing Update.

                var il    = updateMethod.Body.GetILProcessor();
                var first = updateMethod.Body.Instructions[0];

                il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
                il.InsertBefore(first, Instruction.Create(isVirtual ? OpCodes.Callvirt : OpCodes.Call, onDrawGizmos));

                _wovenCount++;

                return;
            }

            var (baseUpdate, baseOwnerRef, baseOwnerDef) = FindNearestBaseMethod(type, UpdateName);

            if (baseUpdate != null && UsesRuntimeGizmosInHierarchy(baseOwnerDef))
            {
                return;
            }

            if (baseUpdate != null && !IsBaseUpdateCallable(type, baseUpdate, baseOwnerDef))
            {
                return;
            }

            var (generatedOnDrawGizmos, generatedIsVirtual) = FindCallableOnDrawGizmos(type);

            if (generatedOnDrawGizmos == null)
            {
                return;
            }

            GenerateUpdate(type, baseUpdate, baseOwnerRef, generatedOnDrawGizmos, generatedIsVirtual);

            _wovenCount++;
        }

        private void GenerateUpdate(TypeDef   type,
                                    MethodDef baseUpdate,
                                    TypeRef   baseOwnerRef,
                                    MethodRef onDrawGizmos,
                                    bool      onDrawGizmosIsVirtual)
        {
            var update = new MethodDef(UpdateName,
                                       MethodAttributes.Private | MethodAttributes.HideBySig,
                                       _module.TypeSystem.Void);

            var il = update.Body.GetILProcessor();

            if (baseUpdate != null)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, ImportMethod(baseUpdate, baseOwnerRef));
            }

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(onDrawGizmosIsVirtual ? OpCodes.Callvirt : OpCodes.Call, onDrawGizmos);
            il.Emit(OpCodes.Ret);

            type.Methods.Add(update);
        }

        private bool HasOnDrawGizmosCall(MethodDef update)
        {
            foreach (var instruction in update.Body.Instructions)
            {
                if (instruction.Operand is not MethodRef method
                || !method.HasThis
                ||  method.Parameters.Count != 0)
                {
                    continue;
                }

                if (method.Name == OnDrawGizmosName)
                {
                    return true;
                }

                if (method.Name == UpdateName && UsesRuntimeGizmosInHierarchy(SafeResolve(method.DeclaringType)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsBaseUpdateCallable(TypeDef type, MethodDef baseUpdate, TypeDef owner)
        {
            if (owner.Module == _module)
            {
                if (baseUpdate.IsPrivate)
                {
                    baseUpdate.IsPrivate = false;
                    baseUpdate.IsFamily  = true;

                    _wovenCount++;
                }

                return true;
            }

            if (baseUpdate.IsPublic || baseUpdate.IsFamily || baseUpdate.IsFamilyOrAssembly)
            {
                return true;
            }

            _diagnostics.Add(Error(
                $"'{type.FullName}' inherits an inaccessible Update() from '{owner.FullName}' (another assembly). "
              + "Declare 'void Update()' and call OnDrawGizmos() yourself, make the base Update protected, "
              + "or draw RuntimeGizmos from a separate MonoBehaviour."));

            return false;
        }

        private (MethodRef method, bool isVirtual) FindCallableOnDrawGizmos(TypeDef type)
        {
            TypeRef currentRef = type;
            TypeDef currentDef = type;

            while (currentDef != null && currentDef.FullName != MonoBehaviourName)
            {
                var method = GetDeclaredMethod(currentDef, OnDrawGizmosName);

                if (method != null && !method.IsStatic)
                {
                    var sameModule = currentDef.Module == _module;
                    var accessible = sameModule
                                  || method.IsPublic
                                  || method.IsFamily
                                  || method.IsFamilyOrAssembly
                                  || (method.IsPrivate && UsesRuntimeGizmos(method)); // Widened to protected in its own assembly.

                    if (accessible)
                    {
                        return (ImportMethod(method, currentRef), method.IsVirtual);
                    }
                }

                currentRef = GetBaseTypeReference(currentRef, currentDef);
                currentDef = SafeResolve(currentRef);
            }

            _diagnostics.Add(Error($"No callable OnDrawGizmos was found for '{type.FullName}'."));

            return (null, false);
        }

        private static (MethodDef method, TypeRef ownerRef, TypeDef ownerDef) FindNearestBaseMethod(TypeDef type, string name)
        {
            var currentRef = GetBaseTypeReference(type, type);

            while (currentRef != null)
            {
                var currentDef = SafeResolve(currentRef);

                if (currentDef == null || currentDef.FullName == MonoBehaviourName)
                {
                    break;
                }

                var method = GetDeclaredMethod(currentDef, name);

                if (method != null)
                {
                    return (method, currentRef, currentDef);
                }

                currentRef = GetBaseTypeReference(currentRef, currentDef);
            }

            return (null, null, null);
        }

        private bool UsesRuntimeGizmosInHierarchy(TypeDef type)
        {
            if (type == null || type.FullName == MonoBehaviourName)
            {
                return false;
            }

            if (_usesRuntimeGizmosByType.TryGetValue(type, out var cached))
            {
                return cached;
            }

            _usesRuntimeGizmosByType[type] = false; // Guards against cyclic bases in broken IL.

            var method = GetDeclaredMethod(type, OnDrawGizmosName);
            var result = (method != null && UsesRuntimeGizmos(method))
                       || UsesRuntimeGizmosInHierarchy(SafeResolve(type.BaseType));

            _usesRuntimeGizmosByType[type] = result;

            return result;
        }

        private static bool UsesRuntimeGizmos(MethodDef method)
        {
            if (!method.HasBody)
            {
                return false;
            }

            foreach (var instruction in method.Body.Instructions)
            {
                TypeRef owner;

                switch (instruction.Operand)
                {
                    case MethodRef      methodRef: owner = methodRef.DeclaringType; break;
                    case FieldReference fieldRef:  owner = fieldRef.DeclaringType;  break;
                    case TypeRef        typeRef:   owner = typeRef;                 break;
                    default: continue;
                }

                if (owner is GenericInstanceType genericInstance)
                {
                    owner = genericInstance.ElementType;
                }

                while (owner.DeclaringType != null)
                {
                    owner = owner.DeclaringType;
                }

                if (owner.FullName != GizmosTypeName)
                {
                    continue;
                }

                var scopeName = owner.Scope switch
                {
                    AssemblyNameReference assemblyName => assemblyName.Name,
                    ModuleDefinition      module       => module.Assembly?.Name?.Name,
                    _                                  => owner.Scope?.Name,
                };

                if (scopeName == GizmosAssemblyName)
                {
                    return true;
                }
            }

            return false;
        }

        private MethodRef ImportMethod(MethodDef method, TypeRef declaringTypeRef)
        {
            if (method.Module == _module && declaringTypeRef is not GenericInstanceType)
            {
                return method;
            }

            var methodRef = new MethodRef(method.Name,
                                          _module.ImportReference(method.ReturnType),
                                          _module.ImportReference(declaringTypeRef))
            {
                HasThis           = method.HasThis,
                ExplicitThis      = method.ExplicitThis,
                CallingConvention = method.CallingConvention,
            };

            return _module.ImportReference(methodRef);
        }

        private static TypeRef GetBaseTypeReference(TypeRef currentRef, TypeDef currentDef)
        {
            var baseRef = currentDef.BaseType;

            if (baseRef == null)
            {
                return null;
            }

            if (currentRef is GenericInstanceType current
            &&  baseRef    is GenericInstanceType baseGeneric)
            {
                var instantiated = new GenericInstanceType(baseGeneric.ElementType);

                foreach (var argument in baseGeneric.GenericArguments)
                {
                    instantiated.GenericArguments.Add(argument is GenericParameter parameter
                                                   && parameter.Owner == currentDef
                                                    ? current.GenericArguments[parameter.Position]
                                                    : argument);
                }

                return instantiated;
            }

            return baseRef;
        }

        private static MethodDef GetDeclaredMethod(TypeDef type, string name)
        {
            foreach (var method in type.Methods)
            {
                if (method.Name == name
                &&  method.HasThis
                && !method.HasParameters
                && !method.HasGenericParameters
                &&  method.ReturnType.FullName == VoidTypeName)
                {
                    return method;
                }
            }

            return null;
        }

        private static TypeDef SafeResolve(TypeRef typeRef)
        {
            try
            {
                return typeRef?.Resolve();
            }
            catch (AssemblyResolutionException)
            {
                return null;
            }
        }
    }
}}