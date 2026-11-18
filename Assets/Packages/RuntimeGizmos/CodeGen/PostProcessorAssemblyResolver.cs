using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Mono.Cecil;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace RuntimeGizmosCodeGen {
internal sealed class PostProcessorAssemblyResolver : IAssemblyResolver
{
    private readonly ICompiledAssembly                      _compiledAssembly;
    private readonly Dictionary<string, HashSet<string>>    _referenceToPathMap   = new ();
    private readonly HashSet<string>                        _referenceDirectories = new ();
    private readonly Dictionary<string, AssemblyDefinition> _cache                = new ();

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

            return paths.FirstOrDefault(path => AssemblyName.GetAssemblyName(path).FullName == name.FullName);
        }

        foreach (var directory in _referenceDirectories)
        {
            var candidate = Path.Combine(directory, name.Name + ".dll");

            if (!File.Exists(candidate))
            {
                continue;
            }

            if (!_referenceToPathMap.TryGetValue(name.Name, out var fileList))
            {
                fileList = new HashSet<string>();

                _referenceToPathMap.Add(name.Name, fileList);
            }

            fileList.Add(candidate);

            return candidate;
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
}}