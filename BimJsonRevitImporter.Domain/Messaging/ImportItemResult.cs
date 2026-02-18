namespace BimJsonRevitImporter.Domain.Messaging
{
    public class ImportItemResult
    {
        public string SourceWallId { get; set; }
        public ImportItemStatus Status { get; set; }
        public string ReasonCode { get; set; }
        public string Message { get; set; }
        public string CreatedElementIdString { get; set; }
    }
}
