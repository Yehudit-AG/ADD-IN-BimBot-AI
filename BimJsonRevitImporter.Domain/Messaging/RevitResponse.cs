using System;
using System.Collections.Generic;

namespace BimJsonRevitImporter.Domain.Messaging
{
    public class RevitResponse
    {
        public Guid CorrelationId { get; set; }
        public bool Success { get; set; }
        public object Payload { get; set; }
        public List<Diagnostic> Diagnostics { get; set; } = new List<Diagnostic>();
    }
}
