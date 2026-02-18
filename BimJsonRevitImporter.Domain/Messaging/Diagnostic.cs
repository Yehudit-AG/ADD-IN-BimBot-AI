namespace BimJsonRevitImporter.Domain.Messaging
{
    public class Diagnostic
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public DiagnosticSeverity Severity { get; set; }
    }
}
