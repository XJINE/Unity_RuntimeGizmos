using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

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
        private readonly ModuleDefinition                 _module;
        private readonly List<DiagnosticMessage>          _diagnostics;
        private readonly Dictionary<TypeDefinition, bool> _needsGizmosByType = new ();

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

        private static bool IsProcessType(TypeDefinition type)
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

                var definition = SafeResolve(current);

                if (definition == null)
                {
                    return false;
                }

                current = definition.BaseType;
            }

            return false;
        }

        private void ProcessType(TypeDefinition type)
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

            if (!NeedsGizmos(type))
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

                var onDrawGizmos = ResolveOnDrawGizmos(type, out var isVirtual);

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

            var baseUpdate = FindNearestBaseMethod(type, UpdateName, out var baseOwnerReference, out var baseOwnerDefinition);

            if (baseUpdate != null && NeedsGizmos(baseOwnerDefinition))
            {
                return;
            }

            if (baseUpdate != null && !IsBaseUpdateCallable(type, baseUpdate, baseOwnerDefinition))
            {
                return;
            }

            var generatedOnDrawGizmos = ResolveOnDrawGizmos(type, out var generatedIsVirtual);

            if (generatedOnDrawGizmos == null)
            {
                return;
            }

            GenerateUpdate(type, baseUpdate, baseOwnerReference, generatedOnDrawGizmos, generatedIsVirtual);

            _wovenCount++;
        }

        private void GenerateUpdate(TypeDefinition   type,
                                    MethodDefinition baseUpdate,
                                    TypeReference    baseOwnerReference,
                                    MethodReference  onDrawGizmos,
                                    bool             onDrawGizmosIsVirtual)
        {
            var update = new MethodDefinition(UpdateName,
                                              MethodAttributes.Private | MethodAttributes.HideBySig,
                                              _module.TypeSystem.Void);

            var il = update.Body.GetILProcessor();

            if (baseUpdate != null)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, ImportMethod(baseUpdate, baseOwnerReference));
            }

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(onDrawGizmosIsVirtual ? OpCodes.Callvirt : OpCodes.Call, onDrawGizmos);
            il.Emit(OpCodes.Ret);

            type.Methods.Add(update);
        }

        private bool HasOnDrawGizmosCall(MethodDefinition update)
        {
            foreach (var instruction in update.Body.Instructions)
            {
                if (instruction.Operand is not MethodReference method
                || !method.HasThis
                ||  method.Parameters.Count != 0)
                {
                    continue;
                }

                if (method.Name == OnDrawGizmosName)
                {
                    return true;
                }

                if (method.Name == UpdateName && NeedsGizmos(SafeResolve(method.DeclaringType)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsBaseUpdateCallable(TypeDefinition type, MethodDefinition baseUpdate, TypeDefinition owner)
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
                $"'{type.FullName}' draws RuntimeGizmos in OnDrawGizmos "
              + $"but inherits an inaccessible 'Update()' from '{owner.FullName}' in another assembly, "
              + "so a safe Update cannot be generated. "
              + $"Either declare 'void Update()' in '{type.FullName}' and call 'OnDrawGizmos();' from it, "
              + "make the base 'Update' protected, "
              + "or move the RuntimeGizmos drawing to a separate MonoBehaviour."));

            return false;
        }

        // NOTE:
        // Finds the OnDrawGizmos that instances of "type" should run: the most derived
        // declaration, like Unity's native gizmo pass. Declarations that stay inaccessible
        // at runtime (non-RuntimeGizmos-using private methods in other assemblies) are
        // skipped; RuntimeGizmos-using private ones are widened by their own assembly.
        private MethodReference ResolveOnDrawGizmos(TypeDefinition type, out bool isVirtual)
        {
            TypeReference  currentReference  = type;
            TypeDefinition currentDefinition = type;

            while (currentDefinition != null && currentDefinition.FullName != MonoBehaviourName)
            {
                var method = GetDeclaredMethod(currentDefinition, OnDrawGizmosName);

                if (method != null && !method.IsStatic)
                {
                    var sameModule = currentDefinition.Module == _module;
                    var accessible = sameModule
                                  || method.IsPublic
                                  || method.IsFamily
                                  || method.IsFamilyOrAssembly
                                  || (method.IsPrivate && UsesRuntimeGizmos(method)); // Widened to protected in its own assembly.

                    if (accessible)
                    {
                        isVirtual = method.IsVirtual;

                        return ImportMethod(method, currentReference);
                    }
                }

                currentReference  = GetBaseTypeReference(currentReference, currentDefinition);
                currentDefinition = SafeResolve(currentReference);
            }

            _diagnostics.Add(Error($"No callable OnDrawGizmos was found for '{type.FullName}'."));

            isVirtual = false;

            return null;
        }

        private static MethodDefinition FindNearestBaseMethod(TypeDefinition     type,
                                                              string             name,
                                                              out TypeReference  ownerReference,
                                                              out TypeDefinition ownerDefinition)
        {
            var currentReference = GetBaseTypeReference(type, type);

            while (currentReference != null)
            {
                var currentDefinition = SafeResolve(currentReference);

                if (currentDefinition == null || currentDefinition.FullName == MonoBehaviourName)
                {
                    break;
                }

                var method = GetDeclaredMethod(currentDefinition, name);

                if (method != null)
                {
                    ownerReference  = currentReference;
                    ownerDefinition = currentDefinition;

                    return method;
                }

                currentReference = GetBaseTypeReference(currentReference, currentDefinition);
            }

            ownerReference  = null;
            ownerDefinition = null;

            return null;
        }

        private bool NeedsGizmos(TypeDefinition type)
        {
            if (type == null || type.FullName == MonoBehaviourName)
            {
                return false;
            }

            if (_needsGizmosByType.TryGetValue(type, out var cached))
            {
                return cached;
            }

            _needsGizmosByType[type] = false; // Guards against cyclic bases in broken IL.

            var method = GetDeclaredMethod(type, OnDrawGizmosName);
            var result = (method != null && UsesRuntimeGizmos(method))
                       || NeedsGizmos(SafeResolve(type.BaseType));

            _needsGizmosByType[type] = result;

            return result;
        }

        private static bool UsesRuntimeGizmos(MethodDefinition method)
        {
            if (!method.HasBody)
            {
                return false;
            }

            foreach (var instruction in method.Body.Instructions)
            {
                TypeReference owner;

                switch (instruction.Operand)
                {
                    case MethodReference methodReference: owner = methodReference.DeclaringType; break;
                    case FieldReference  fieldReference:  owner = fieldReference.DeclaringType;  break;
                    case TypeReference   typeReference:   owner = typeReference;                 break;
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

        private MethodReference ImportMethod(MethodDefinition method, TypeReference declaringTypeReference)
        {
            if (method.Module == _module && declaringTypeReference is not GenericInstanceType)
            {
                return method;
            }

            var reference = new MethodReference(method.Name,
                                                _module.ImportReference(method.ReturnType),
                                                _module.ImportReference(declaringTypeReference))
            {
                HasThis           = method.HasThis,
                ExplicitThis      = method.ExplicitThis,
                CallingConvention = method.CallingConvention,
            };

            return _module.ImportReference(reference);
        }

        // NOTE:
        // Returns the base type as a TypeReference, substituting the generic arguments of
        // "currentReference" into the base's generic parameters (e.g. Foo : Singleton<Foo>).
        private static TypeReference GetBaseTypeReference(TypeReference  currentReference,
                                                          TypeDefinition currentDefinition)
        {
            var baseReference = currentDefinition.BaseType;

            if (baseReference == null)
            {
                return null;
            }

            if (currentReference is GenericInstanceType current
            &&  baseReference    is GenericInstanceType baseGeneric)
            {
                var instantiated = new GenericInstanceType(baseGeneric.ElementType);

                foreach (var argument in baseGeneric.GenericArguments)
                {
                    instantiated.GenericArguments.Add(argument is GenericParameter parameter
                                                   && parameter.Owner == currentDefinition
                                                    ? current.GenericArguments[parameter.Position]
                                                    : argument);
                }

                return instantiated;
            }

            return baseReference;
        }

        private static MethodDefinition GetDeclaredMethod(TypeDefinition type, string name)
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

        private static TypeDefinition SafeResolve(TypeReference reference)
        {
            try
            {
                return reference?.Resolve();
            }
            catch (AssemblyResolutionException)
            {
                return null;
            }
        }
    }
}}