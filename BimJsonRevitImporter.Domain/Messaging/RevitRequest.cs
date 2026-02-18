using System;

namespace BimJsonRevitImporter.Domain.Messaging
{
    public class RevitRequest
    {
        public RevitRequestType Type { get; set; }
        public object Payload { get; set; }
        public Guid CorrelationId { get; set; }
    }
}
