using System.Collections.Generic;

namespace BimJsonRevitImporter.Domain.Messaging
{
    public class GetSelectableDataPayload
    {
        public List<LevelInfo> Levels { get; set; } = new List<LevelInfo>();
        public List<WallTypeInfo> WallTypes { get; set; } = new List<WallTypeInfo>();
        public List<CadInstanceInfo> CadInstances { get; set; } = new List<CadInstanceInfo>();
    }
}
