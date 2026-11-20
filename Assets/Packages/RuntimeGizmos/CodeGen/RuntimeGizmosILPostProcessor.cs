using System;
using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.CompilationPipeline.Common.Diagnostics;
using Unity.CompilationPipeline.Common.ILPostProcessing;

namespace RuntimeGizmos.CodeGen
{
    // NOTE:
    // Injects an "OnDrawGizmos()" call into "Update()" at build time,
    // so that OnDrawGizmos also runs in player builds.
    // This processor runs for player builds only. In the editor it does nothing;

    internal sealed partial class RuntimeGizmosILPostProcessor : ILPostProcessor
    {
        private static readonly string SelfAssemblyName  = typeof(RuntimeGizmosILPostProcessor).Assembly.GetName().Name;
        private static readonly string MonoBehaviourName = typeof(UnityEngine.MonoBehaviour).FullName;
        private static readonly string VoidTypeName      = typeof(void).FullName;

        private const string GizmosAssemblyName = nameof(RuntimeGizmos);
        private const string GizmosTypeName     = nameof(RuntimeGizmos);
        private const string OnDrawGizmosName   = "OnDrawGizmos";
        private const string UpdateName         = "Update";
        private const string EditorDefine       = "UNITY_EDITOR";

        public override ILPostProcessor GetInstance() => this;

        public override bool WillProcess(ICompiledAssembly compiledAssembly)
        {
            if (compiledAssembly.Name == SelfAssemblyName
             || compiledAssembly.Name == GizmosAssemblyName)
            {
                return false;
            }

            // NOTE:
            // Weave player builds only. Editor assemblies are compiled with 'UNITY_EDITOR',
            // so this also excludes other CodeGen assemblies that reference RuntimeGizmos.
            if (compiledAssembly.Defines != null
             && 0 <= Array.IndexOf(compiledAssembly.Defines, EditorDefine))
            {
                return false;
            }

            var inMemory = compiledAssembly.InMemoryAssembly;

            if (inMemory?.PeData == null || inMemory.PdbData == null)
            {
                // NOTE:
                // Non-null by Unity's contract, but checked defensively since the implementation is external.
                return false;
            }

            foreach (var reference in compiledAssembly.References)
            {
                if (Path.GetFileNameWithoutExtension(reference) == GizmosAssemblyName)
                {
                    return true;
                }
            }

            return false;
        }

        public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)
        {
            var diagnostics = new List<DiagnosticMessage>();

            ReferenceAssemblyResolver resolver = null;
            AssemblyDefinition        assembly = null;

            try
            {
                resolver      = new ReferenceAssemblyResolver(compiledAssembly.References);
                assembly      = ReadAssembly(compiledAssembly, resolver);
                resolver.Self = assembly;

                var woven = new Weaver(assembly.MainModule, diagnostics).Run();

                if (woven == 0 || HasError(diagnostics))
                {
                    return new ILPostProcessResult(null, diagnostics);
                }

                var pe  = new MemoryStream();
                var pdb = new MemoryStream();

                assembly.Write(pe, new WriterParameters
                {
                    SymbolWriterProvider = new PortablePdbWriterProvider(),
                    SymbolStream         = pdb,
                    WriteSymbols         = true,
                });

                return new ILPostProcessResult(new InMemoryAssembly(pe.ToArray(), pdb.ToArray()), diagnostics);
            }
            catch (Exception e)
            {
                diagnostics.Add(Warning($"Skipped '{compiledAssembly.Name}': {e}"));

                return new ILPostProcessResult(null, diagnostics);
            }
            finally
            {
                assembly?.Dispose();
                resolver?.Dispose();
            }
        }

        private static AssemblyDefinition ReadAssembly(ICompiledAssembly compiledAssembly, IAssemblyResolver resolver)
        {
            var readerParameters = new ReaderParameters
            {
                AssemblyResolver     = resolver,
                ReadSymbols          = true,
                SymbolReaderProvider = new PortablePdbReaderProvider(),
                SymbolStream         = new MemoryStream(compiledAssembly.InMemoryAssembly.PdbData),
                ReadingMode          = ReadingMode.Immediate,
            };

            var peStream = new MemoryStream(compiledAssembly.InMemoryAssembly.PeData);

            return AssemblyDefinition.ReadAssembly(peStream, readerParameters);
        }

        private static bool HasError(List<DiagnosticMessage> diagnostics)
        {
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.DiagnosticType == DiagnosticType.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static DiagnosticMessage Error(string message)
        {
            return new DiagnosticMessage
            {
                DiagnosticType = DiagnosticType.Error,
                MessageData    = $"[{SelfAssemblyName}] {message}",
            };
        }

        private static DiagnosticMessage Warning(string message)
        {
            return new DiagnosticMessage
            {
                DiagnosticType = DiagnosticType.Warning,
                MessageData    = $"[{SelfAssemblyName}] {message}",
            };
        }
    }
}