using Unity.CompilationPipeline.Common.Diagnostics;

namespace RuntimeGizmosCodeGen {
internal static class WeavingDiagnostics
{
    public const string GizmosAssemblyName = "RuntimeGizmos";
    public const string GizmosTypeName     = "RuntimeGizmos";
    public const string OnDrawGizmosName   = "OnDrawGizmos";
    public const string UpdateName         = "Update";
    public const string MonoBehaviourName  = "UnityEngine.MonoBehaviour";

    public static DiagnosticMessage Error(string message)
    {
        return new DiagnosticMessage
        {
            DiagnosticType = DiagnosticType.Error,
            MessageData    = message
        };
    }
}}