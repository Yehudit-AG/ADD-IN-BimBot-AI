using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using BimJsonRevitImporter.Domain.Messaging;

namespace BimJsonRevitImporter.Revit.Services
{
    public class RevitQueryService
    {
        public List<LevelInfo> GetLevels(Document doc)
        {
            var list = new List<LevelInfo>();
            if (doc == null) return list;
            var collector = new FilteredElementCollector(doc).OfClass(typeof(Level));
            foreach (Element el in collector)
            {
                if (!(el is Level level)) continue;
                list.Add(new LevelInfo
                {
                    Name = level.Name,
                    ElevationFeet = level.Elevation,
                    UniqueId = level.UniqueId,
                    ElementIdString = level.Id.IntegerValue.ToString()
                });
            }
            list.Sort((a, b) => a.ElevationFeet.CompareTo(b.ElevationFeet));
            return list;
        }

        public List<WallTypeInfo> GetWallTypes(Document doc)
        {
            var list = new List<WallTypeInfo>();
            if (doc == null) return list;
            var collector = new FilteredElementCollector(doc).OfClass(typeof(WallType));
            foreach (Element el in collector)
            {
                if (!(el is WallType wt)) continue;
                var widthParam = wt.get_Parameter(BuiltInParameter.WALL_ATTR_WIDTH_PARAM);
                double widthFeet = widthParam?.AsDouble() ?? 0;
                list.Add(new WallTypeInfo
                {
                    Name = wt.Name,
                    WidthFeet = widthFeet,
                    ElementIdString = wt.Id.IntegerValue.ToString()
                });
            }
            return list;
        }

        public List<CadInstanceInfo> GetCadImportInstances(Document doc)
        {
            var list = new List<CadInstanceInfo>();
            if (doc == null) return list;
            var collector = new FilteredElementCollector(doc).OfClass(typeof(ImportInstance));
            foreach (Element el in collector)
            {
                if (!(el is ImportInstance imp)) continue;
                list.Add(new CadInstanceInfo
                {
                    Name = imp.Name ?? "Import",
                    ElementIdString = imp.Id.IntegerValue.ToString(),
                    UniqueId = imp.UniqueId
                });
            }
            return list;
        }

        /// <summary>ImportInstance → GetTransform(); RevitLinkInstance → GetTotalTransform().</summary>
        public Transform GetCadInstanceTransform(Document doc, ElementId cadId)
        {
            if (doc == null || cadId == null || cadId == ElementId.InvalidElementId) return null;
            var el = doc.GetElement(cadId);
            if (el is ImportInstance imp)
                return imp.GetTransform();
            if (el is RevitLinkInstance link)
                return link.GetTotalTransform();
            return null;
        }
    }
}
