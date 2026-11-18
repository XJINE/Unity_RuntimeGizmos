using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using static RuntimeGizmosCodeGen.WeavingDiagnostics;

namespace RuntimeGizmosCodeGen {
internal sealed class RuntimeGizmosWeaver
{
    private readonly ModuleDefinition                 _module;
    private readonly List<DiagnosticMessage>          _diagnostics;
    private readonly Dictionary<TypeDefinition, bool> _needsGizmosCache = new ();

    private bool _modified;

    public RuntimeGizmosWeaver(ModuleDefinition module, List<DiagnosticMessage> diagnostics)
    {
        _module      = module;
        _diagnostics = diagnostics;
    }

    public bool Run()
    {
        foreach (var type in _module.GetTypes().ToList())
        {
            if (type.IsInterface || type.IsValueType || !IsMonoBehaviour(type))
            {
                continue;
            }

            ProcessType(type);
        }

        return _modified;
    }

    private void ProcessType(TypeDefinition type)
    {
        var declaredGizmos = GetDeclaredMethod(type, OnDrawGizmosName);

        // Widen a private RuntimeGizmos-using OnDrawGizmos to protected,
        // so that woven Updates in derived classes in other assemblies can call it.
        if (declaredGizmos != null 
         && declaredGizmos.IsPrivate
         && UsesRuntimeGizmos(declaredGizmos))
        {
            declaredGizmos.IsPrivate = false;
            declaredGizmos.IsFamily  = true;
            _modified                = true;
        }

        if (!NeedsGizmos(type))
        {
            return;
        }

        var update = GetDeclaredMethod(type, UpdateName);

        if (update != null)
        {
            if (!update.HasBody || IsCovered(update))
            {
                return;
            }

            var gizmosRef = ResolveOnDrawGizmosRef(type, out var isVirtual);

            if (gizmosRef == null)
            {
                return;
            }

            InjectCall(update, gizmosRef, isVirtual);

            _modified = true;

            return;
        }

        var baseUpdate = FindNearestBaseMethod(type, UpdateName, out var baseOwnerRef, out var baseOwner);

        if (baseUpdate != null && NeedsGizmos(baseOwner))
        {
            return;
        }

        if (baseUpdate != null && !IsBaseUpdateCallable(type, baseUpdate, baseOwner))
        {
            return; // Diagnostic has been emitted.
        }

        var generatedGizmosRef = ResolveOnDrawGizmosRef(type, out var generatedIsVirtual);

        if (generatedGizmosRef == null)
        {
            return;
        }

        GenerateUpdate(type, baseUpdate, baseOwnerRef, generatedGizmosRef, generatedIsVirtual);

        _modified = true;
    }

    private void GenerateUpdate(TypeDefinition   type,
                                MethodDefinition baseUpdate,
                                TypeReference    baseOwnerRef,
                                MethodReference  gizmosRef,
                                bool             gizmosIsVirtual)
    {
        var update = new MethodDefinition(UpdateName,
                                          MethodAttributes.Private | MethodAttributes.HideBySig,
                                          _module.TypeSystem.Void);

        var il = update.Body.GetILProcessor();

        if (baseUpdate != null)
        {
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, ImportMethod(baseUpdate, baseOwnerRef));
        }

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(gizmosIsVirtual ? OpCodes.Callvirt : OpCodes.Call, gizmosRef);
        il.Emit(OpCodes.Ret);

        type.Methods.Add(update);
    }

    private static void InjectCall(MethodDefinition update, MethodReference gizmosRef, bool isVirtual)
    {
        var il    = update.Body.GetILProcessor();
        var first = update.Body.Instructions[0];

        il.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0));
        il.InsertBefore(first, Instruction.Create(isVirtual ? OpCodes.Callvirt : OpCodes.Call, gizmosRef));
    }

    // NOTE:
    // Update is treated as covered when its body already calls OnDrawGizmos
    // (manual v1-style code, or a previous run), or calls a base Update that
    // belongs to a type that is woven itself.
    private bool IsCovered(MethodDefinition update)
    {
        foreach (var instruction in update.Body.Instructions)
        {
            if (!(instruction.Operand is MethodReference method)
             || !method.HasThis
             || method.Parameters.Count != 0)
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
                _modified            = true;
            }

            return true;
        }

        if (baseUpdate.IsPublic || baseUpdate.IsFamily || baseUpdate.IsFamilyOrAssembly)
        {
            return true;
        }

        _diagnostics.Add(Error(
            $"RuntimeGizmos weaving: '{type.FullName}' draws RuntimeGizmos in OnDrawGizmos "
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
    private MethodReference ResolveOnDrawGizmosRef(TypeDefinition type, out bool isVirtual)
    {
        TypeReference  currentRef = type;
        TypeDefinition current    = type;

        while (current != null && current.FullName != MonoBehaviourName)
        {
            var method = GetDeclaredMethod(current, OnDrawGizmosName);

            if (method != null && !method.IsStatic)
            {
                var sameModule = current.Module == _module;
                var accessible = sameModule
                              || method.IsPublic
                              || method.IsFamily
                              || method.IsFamilyOrAssembly
                              || (method.IsPrivate && UsesRuntimeGizmos(method)); // Widened to protected in its own assembly.

                if (accessible)
                {
                    isVirtual = method.IsVirtual;
                    return ImportMethod(method, currentRef);
                }
            }

            currentRef = GetBaseTypeRef(currentRef, current);
            current    = SafeResolve(currentRef);
        }

        _diagnostics.Add(Error($"RuntimeGizmos weaving: no callable OnDrawGizmos was found for '{type.FullName}'."));

        isVirtual = false;

        return null;
    }

    private MethodDefinition FindNearestBaseMethod(TypeDefinition     type,
                                                   string             name,
                                                   out TypeReference  ownerRef,
                                                   out TypeDefinition ownerDef)
    {
        var currentRef = GetBaseTypeRef(type, type);

        while (currentRef != null)
        {
            var current = SafeResolve(currentRef);

            if (current == null || current.FullName == MonoBehaviourName)
            {
                break;
            }

            var method = GetDeclaredMethod(current, name);

            if (method != null)
            {
                ownerRef = currentRef;
                ownerDef = current;

                return method;
            }

            currentRef = GetBaseTypeRef(currentRef, current);
        }

        ownerRef = null;
        ownerDef = null;

        return null;
    }

    private bool NeedsGizmos(TypeDefinition type)
    {
        if (type == null || type.FullName == MonoBehaviourName)
        {
            return false;
        }

        if (_needsGizmosCache.TryGetValue(type, out var cached))
        {
            return cached;
        }

        _needsGizmosCache[type] = false; // Guards against cyclic bases in broken IL.

        var method = GetDeclaredMethod(type, OnDrawGizmosName);
        var result = (method != null
                  && UsesRuntimeGizmos(method))
                  || NeedsGizmos(SafeResolve(type.BaseType));

        _needsGizmosCache[type] = result;

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

            if (owner.FullName == GizmosTypeName && ScopeName(owner) == GizmosAssemblyName)
            {
                return true;
            }
        }

        return false;
    }

    private MethodReference ImportMethod(MethodDefinition method, TypeReference declaringTypeRef)
    {
        if (method.Module == _module && !(declaringTypeRef is GenericInstanceType))
        {
            return method;
        }

        var reference = new MethodReference(method.Name,
                                            _module.ImportReference(method.ReturnType),
                                            _module.ImportReference(declaringTypeRef))
        {
            HasThis           = method.HasThis,
            ExplicitThis      = method.ExplicitThis,
            CallingConvention = method.CallingConvention
        };

        return _module.ImportReference(reference);
    }

    // NOTE:
    // Returns the base type as a TypeReference, substituting the generic arguments of
    // "currentRef" into the base's generic parameters (e.g. Foo : Singleton<Foo>).
    private static TypeReference GetBaseTypeRef(TypeReference currentRef, TypeDefinition currentDef)
    {
        var baseRef = currentDef.BaseType;

        if (baseRef == null)
        {
            return null;
        }

        if (currentRef is GenericInstanceType current && baseRef is GenericInstanceType baseGeneric)
        {
            var instantiated = new GenericInstanceType(baseGeneric.ElementType);

            foreach (var argument in baseGeneric.GenericArguments)
            {
                instantiated.GenericArguments.Add(argument is GenericParameter parameter
                                               && parameter.Owner == currentDef ? current.GenericArguments[parameter.Position]
                                                                                : argument);
            }

            return instantiated;
        }

        return baseRef;
    }

    private static MethodDefinition GetDeclaredMethod(TypeDefinition type, string name)
    {
        return type.Methods.FirstOrDefault(method =>  method.Name == name
                                                  &&  method.HasThis
                                                  && !method.HasParameters
                                                  && !method.HasGenericParameters
                                                  &&  method.ReturnType.FullName == "System.Void");
    }

    private static bool IsMonoBehaviour(TypeDefinition type)
    {
        var current = type.BaseType;

        while (current != null)
        {
            if (current.FullName == MonoBehaviourName)
            {
                return true;
            }

            var resolved = SafeResolve(current);

            if (resolved == null)
            {
                return false;
            }

            current = resolved.BaseType;
        }

        return false;
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

    private static string ScopeName(TypeReference type)
    {
        switch (type.Scope)
        {
            case AssemblyNameReference assemblyName: return assemblyName.Name;
            case ModuleDefinition      module:       return module.Assembly?.Name?.Name;
            default:                                 return type.Scope?.Name;
        }
    }
}}