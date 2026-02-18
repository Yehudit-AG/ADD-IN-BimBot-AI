using System.Collections.Generic;

namespace BimJsonRevitImporter.Domain.Messaging
{
    public class ImportReport
    {
        public int Created { get; set; }
        public int Skipped { get; set; }
        public int Failed { get; set; }
        public List<ImportItemResult> ItemResults { get; set; } = new List<ImportItemResult>();
    }
}
