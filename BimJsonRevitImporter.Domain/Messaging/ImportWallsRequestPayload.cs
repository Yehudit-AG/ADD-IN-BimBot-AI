using System.Collections.Generic;

namespace BimJsonRevitImporter.Domain.Messaging
{
    public class ImportWallsRequestPayload
    {
        public string BimJsonPath { get; set; }
        public string CadElementIdString { get; set; }
        public string BaseLevelElementIdString { get; set; }
        public string TopLevelElementIdString { get; set; }
        public List<WallTypeMappingItem> WallTypeMappings { get; set; } = new List<WallTypeMappingItem>();
    }
}
