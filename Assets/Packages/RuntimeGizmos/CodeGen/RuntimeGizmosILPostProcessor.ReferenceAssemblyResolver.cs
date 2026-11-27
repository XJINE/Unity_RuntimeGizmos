using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace RuntimeGizmos.CodeGen {
internal sealed partial class RuntimeGizmosILPostProcessor : ILPostProcessor
{
    private sealed class ReferenceAssemblyResolver : IAssemblyResolver
    {
        private readonly Dictionary<string, string>             _pathByName  = new (StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string>                        _directories = new (StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AssemblyDefinition> _cache       = new (StringComparer.OrdinalIgnoreCase);

        public AssemblyDefinition Self { get; set; }

        public ReferenceAssemblyResolver(IEnumerable<string> referencePaths)
        {
            foreach (var path in referencePaths)
            {
                _pathByName[Path.GetFileNameWithoutExtension(path)] = path;

                var directory = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(directory))
                {
                    _directories.Add(directory);
                }
            }
        }

        // NOTE:
        // These 'Resolve' overloads and 'Dispose' implement IAssemblyResolver.

        public AssemblyDefinition Resolve(AssemblyNameReference name) => Resolve(name, new ReaderParameters(ReadingMode.Deferred));

        public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters)
        {
            if (Self != null && name.Name == Self.Name.Name)
            {
                return Self;
            }

            if (_cache.TryGetValue(name.Name, out var cached))
            {
                return cached;
            }

            var path = FindPath(name.Name);

            if (path == null)
            {
                return null; // Cecil treats this as "unresolved"; callers handle null.
            }

            parameters.AssemblyResolver = this;

            // NOTE:
            // Unity may rewrite this file, so read it through memory.

            var def = AssemblyDefinition.ReadAssembly(new MemoryStream(File.ReadAllBytes(path)), parameters);

            _cache[name.Name] = def;

            return def;
        }

        private string FindPath(string assemblyName)
        {
            if (_pathByName.TryGetValue(assemblyName, out var path))
            {
                return path;
            }

            // NOTE:
            // The paths passed to this resolver are only direct references (from ICompiledAssembly.References).
            // Transitive references usually sit in the same directories, so fall back to scanning them.

            foreach (var directory in _directories)
            {
                var candidate = Path.Combine(directory, assemblyName + ".dll");

                if (File.Exists(candidate))
                {
                    _pathByName[assemblyName] = candidate;

                    return candidate;
                }
            }

            return null;
        }

        public void Dispose()
        {
            foreach (var def in _cache.Values)
            {
                def.Dispose();
            }

            _cache.Clear();
        }
    }
}}