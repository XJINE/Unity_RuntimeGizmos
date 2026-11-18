using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;
using static RuntimeGizmosCodeGen.WeavingDiagnostics;

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
//
// The weaving itself is implemented in RuntimeGizmosWeaver.
internal sealed class RuntimeGizmosILPostProcessor : ILPostProcessor
{
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

        return compiledAssembly.References.Any(reference => Path.GetFileNameWithoutExtension(reference) == GizmosAssemblyName);
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
                modified = new RuntimeGizmosWeaver(assembly.MainModule, diagnostics).Run();
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
}}