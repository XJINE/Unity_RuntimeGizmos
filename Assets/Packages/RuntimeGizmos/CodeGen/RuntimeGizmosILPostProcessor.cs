using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace RuntimeGizmosCodeGen {
// NOTE:
// Makes RuntimeGizmos drawing written in OnDrawGizmos also run in player builds,
// by injecting an "OnDrawGizmos()" call into "Update()" at build time.
//
// This processor runs for player builds only. In the editor it does nothing;
// there, Unity's native gizmo pass calls OnDrawGizmos.
//
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
internal sealed class RuntimeGizmosILPostProcessor : ILPostProcessor
{
    private const string GizmosAssemblyName   = "RuntimeGizmos";
    private const string GizmosTypeName       = "RuntimeGizmos";
    private const string OnDrawGizmosName     = "OnDrawGizmos";
    private const string UpdateName           = "Update";
    private const string MonoBehaviourName    = "UnityEngine.MonoBehaviour";

    public override ILPostProcessor GetInstance()
    {
        return this;
    }

    public override bool WillProcess(ICompiledAssembly compiledAssembly)
    {
        if (compiledAssembly.Name == GizmosAssemblyName
         || compiledAssembly.Name.EndsWith(".CodeGen", StringComparison.Ordinal))
        {
            return false;
        }

        // Weave player builds only.
        if (compiledAssembly.Defines != null
         && compiledAssembly.Defines.Contains("UNITY_EDITOR"))
        {
            return false;
        }

        if (compiledAssembly.InMemoryAssembly.PeData  == null
         || compiledAssembly.InMemoryAssembly.PdbData == null)
        {
            return false;
        }

        return compiledAssembly.References.Any(
            reference => Path.GetFileNameWithoutExtension(reference) == GizmosAssemblyName);
    }

    public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
    {
        var diagnostics = new List<DiagnosticMessage>();

        AssemblyDefinition assembly;

        try
        {
            assembly = LoadAssembly(compiledAssembly);
        }
        catch (BadImageFormatException)
        {
            return new ILPostProcessResult(null, diagnostics);
        }

        using (assembly)
        {
            bool modified;

            try
            {
                modified = new Weaver(assembly.MainModule, diagnostics).Run();
            }
            catch (Exception exception)
            {
                diagnostics.Add(Error($"RuntimeGizmos weaving failed for '{compiledAssembly.Name}': {exception}"));
                return new ILPostProcessResult(null, diagnostics);
            }

            if (!modified || diagnostics.Any(d => d.DiagnosticType == DiagnosticType.Error))
            {
                return new ILPostProcessResult(null, diagnostics);
            }

            var pe  = new MemoryStream();
            var pdb = new MemoryStream();

            var writerParameters = new WriterParameters
            {
                SymbolWriterProvider = new PortablePdbWriterProvider(),
                SymbolStream         = pdb,
                WriteSymbols         = true
            };

            assembly.Write(pe, writerParameters);

            return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), diagnostics);
        }
    }

    internal static DiagnosticMessage Error(string message)
    {
        return new DiagnosticMessage
        {
            DiagnosticType = DiagnosticType.Error,
            MessageData    = message
        };
    }

    private static AssemblyDefinition LoadAssembly(ICompiledAssembly compiledAssembly)
    {
        var resolver = new PostProcessorAssemblyResolver(compiledAssembly);

        var readerParameters = new ReaderParameters
        {
            SymbolStream         = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData),
            SymbolReaderProvider = new PortablePdbReaderProvider(),
            AssemblyResolver     = resolver,
            ReadingMode          = ReadingMode.Immediate
        };

        var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData);
        var assembly = AssemblyDefinition.ReadAssembly(peStream, readerParameters);

        resolver.SetSelfAssembly(assembly);

        return assembly;
    }

    private sealed class Weaver
    {
        private readonly ModuleDefinition                 _module;
        private readonly List<DiagnosticMessage>          _diagnostics;
        private readonly Dictionary<TypeDefinition, bool> _needsGizmosCache = new Dictionary<TypeDefinition, bool>();

        private bool _modified;

        public Weaver(ModuleDefinition module, List<DiagnosticMessage> diagnostics)
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

            // Widen a private RuntimeGizmos-using OnDrawGizmos to protected, so that
            // woven Updates in derived classes in other assemblies can call it.
            if (declaredGizmos != null && declaredGizmos.IsPrivate && UsesRuntimeGizmos(declaredGizmos))
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
                // The inherited Update is woven in its own assembly and already calls
                // OnDrawGizmos (virtual overrides are dispatched via callvirt).
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

        private void GenerateUpdate(TypeDefinition  type,
                                    MethodDefinition baseUpdate,
                                    TypeReference    baseOwnerRef,
                                    MethodReference  gizmosRef,
                                    bool             gizmosIsVirtual)
        {
            var update = new MethodDefinition(UpdateName,
                                              Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.HideBySig,
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

            _diagnostics.Add(Error(
                $"RuntimeGizmos weaving: no callable OnDrawGizmos was found for '{type.FullName}'."));

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
            var result = (method != null && UsesRuntimeGizmos(method))
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
                    default:                              continue;
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
                    instantiated.GenericArguments.Add(
                        argument is GenericParameter parameter && parameter.Owner == currentDef
                            ? current.GenericArguments[parameter.Position]
                            : argument);
                }

                return instantiated;
            }

            return baseRef;
        }

        private static MethodDefinition GetDeclaredMethod(TypeDefinition type, string name)
        {
            foreach (var method in type.Methods)
            {
                if (method.Name == name
                 && method.HasThis
                 && !method.HasParameters
                 && !method.HasGenericParameters
                 && method.ReturnType.FullName == "System.Void")
                {
                    return method;
                }
            }

            return null;
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
    }

    // NOTE:
    // Standard ILPostProcessor assembly resolver: resolves references from the compiled
    // assembly's reference list, and indirect references from the same directories.
    private sealed class PostProcessorAssemblyResolver : IAssemblyResolver
    {
        private readonly ICompiledAssembly                      _compiledAssembly;
        private readonly Dictionary<string, HashSet<string>>    _referenceToPathMap = new Dictionary<string, HashSet<string>>();
        private readonly HashSet<string>                        _referenceDirectories = new HashSet<string>();
        private readonly Dictionary<string, AssemblyDefinition> _cache = new Dictionary<string, AssemblyDefinition>();

        private AssemblyDefinition _selfAssembly;

        public PostProcessorAssemblyResolver(ICompiledAssembly compiledAssembly)
        {
            _compiledAssembly = compiledAssembly;

            foreach (var reference in compiledAssembly.References)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(reference);

                if (!_referenceToPathMap.TryGetValue(assemblyName, out var fileList))
                {
                    fileList = new HashSet<string>();
                    _referenceToPathMap.Add(assemblyName, fileList);
                }

                fileList.Add(reference);
                _referenceDirectories.Add(Path.GetDirectoryName(reference));
            }
        }

        public void SetSelfAssembly(AssemblyDefinition assembly)
        {
            _selfAssembly = assembly;
        }

        public void Dispose()
        {
            foreach (var assembly in _cache.Values)
            {
                assembly.Dispose();
            }

            _cache.Clear();
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            return Resolve(name, new ReaderParameters(ReadingMode.Deferred));
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            if (name.Name == _compiledAssembly.Name)
            {
                return _selfAssembly;
            }

            var fileName = FindFile(name);

            if (fileName == null)
            {
                return null;
            }

            if (_cache.TryGetValue(fileName, out var cached))
            {
                return cached;
            }

            parameters.AssemblyResolver = this;

            var peStream = MemoryStreamFor(fileName);
            var pdbPath  = fileName + ".pdb";

            if (File.Exists(pdbPath))
            {
                parameters.SymbolStream = MemoryStreamFor(pdbPath);
            }

            var assembly = AssemblyDefinition.ReadAssembly(peStream, parameters);
            _cache.Add(fileName, assembly);

            return assembly;
        }

        private string FindFile(AssemblyNameReference name)
        {
            if (_referenceToPathMap.TryGetValue(name.Name, out var paths))
            {
                if (paths.Count == 1)
                {
                    return paths.First();
                }

                foreach (var path in paths)
                {
                    if (AssemblyName.GetAssemblyName(path).FullName == name.FullName)
                    {
                        return path;
                    }
                }

                return null;
            }

            // ICompiledAssembly only lists direct references. Indirect references are
            // located next to direct ones, so search those directories as well.
            foreach (var directory in _referenceDirectories)
            {
                var candidate = Path.Combine(directory, name.Name + ".dll");

                if (File.Exists(candidate))
                {
                    if (!_referenceToPathMap.TryGetValue(name.Name, out var fileList))
                    {
                        fileList = new HashSet<string>();
                        _referenceToPathMap.Add(name.Name, fileList);
                    }

                    fileList.Add(candidate);
                    return candidate;
                }
            }

            return null;
        }

        private static MemoryStream MemoryStreamFor(string fileName)
        {
            return Retry(10, TimeSpan.FromSeconds(1), () =>
            {
                byte[] bytes;

                using (var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    bytes = new byte[fileStream.Length];

                    if (fileStream.Read(bytes, 0, (int)fileStream.Length) != fileStream.Length)
                    {
                        throw new InvalidOperationException("File read length is not full length of file.");
                    }
                }

                return new MemoryStream(bytes);
            });
        }

        private static MemoryStream Retry(int retryCount, TimeSpan waitTime, Func<MemoryStream> func)
        {
            try
            {
                return func();
            }
            catch (IOException)
            {
                if (retryCount == 0)
                {
                    throw;
                }

                Thread.Sleep(waitTime);
                return Retry(retryCount - 1, waitTime, func);
            }
        }
    }
}}