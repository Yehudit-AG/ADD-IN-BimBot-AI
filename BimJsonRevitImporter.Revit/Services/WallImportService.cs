using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using BimJsonRevitImporter.Domain.BimJson;
using BimJsonRevitImporter.Domain.Messaging;

namespace BimJsonRevitImporter.Revit.Services
{
    public class WallImportService
    {
        /// <summary>Minimum wall length threshold in mm (constant).</summary>
        public const double MinWallLengthMm = 100;

        public ImportReport ImportWalls(Document doc, ImportWallsRequestPayload payload)
        {
            var report = new ImportReport();
            if (doc == null || payload == null)
            {
                report.Failed++;
                report.ItemResults.Add(new ImportItemResult { SourceWallId = "", Status = ImportItemStatus.Failed, ReasonCode = "NO_PAYLOAD", Message = "Document or payload is null." });
                return report;
            }

            var walls = BimJsonReader.ReadWalls(payload.BimJsonPath);
            if (walls.Count == 0)
                return report;

            int baseLevelIdInt;
            int topLevelIdInt;
            int cadIdInt;
            if (!int.TryParse(payload.BaseLevelElementIdString, out baseLevelIdInt) ||
                !int.TryParse(payload.TopLevelElementIdString, out topLevelIdInt) ||
                !int.TryParse(payload.CadElementIdString, out cadIdInt))
            {
                report.Failed = walls.Count;
                foreach (var w in walls)
                    report.ItemResults.Add(new ImportItemResult { SourceWallId = w.Id, Status = ImportItemStatus.Failed, ReasonCode = "INVALID_ID", Message = "Invalid level or CAD element id." });
                return report;
            }

            var baseLevelId = new ElementId(baseLevelIdInt);
            var topLevelId = new ElementId(topLevelIdInt);
            var cadId = new ElementId(cadIdInt);

            Level baseLevel = doc.GetElement(baseLevelId) as Level;
            Level topLevel = doc.GetElement(topLevelId) as Level;
            if (baseLevel == null || topLevel == null)
            {
                report.Failed = walls.Count;
                foreach (var w in walls)
                    report.ItemResults.Add(new ImportItemResult { SourceWallId = w.Id, Status = ImportItemStatus.Failed, ReasonCode = "LEVEL_NOT_FOUND", Message = "Base or top level not found." });
                return report;
            }

            if (topLevel.Elevation < baseLevel.Elevation)
            {
                report.Failed = walls.Count;
                foreach (var w in walls)
                    report.ItemResults.Add(new ImportItemResult { SourceWallId = w.Id, Status = ImportItemStatus.Failed, ReasonCode = "TOP_BELOW_BASE", Message = "Top level elevation is below base level." });
                return report;
            }

            var mappingByThickness = new Dictionary<double, ElementId>();
            foreach (var m in payload.WallTypeMappings ?? new List<WallTypeMappingItem>())
            {
                var key = Math.Round(m.ThicknessCm, 2);
                if (int.TryParse(m.WallTypeElementIdString, out int tid))
                    mappingByThickness[key] = new ElementId(tid);
            }

            var queryService = new RevitQueryService();
            var transform = queryService.GetCadInstanceTransform(doc, cadId);
            if (transform == null)
            {
                report.Failed = walls.Count;
                foreach (var w in walls)
                    report.ItemResults.Add(new ImportItemResult { SourceWallId = w.Id, Status = ImportItemStatus.Failed, ReasonCode = "CAD_TRANSFORM", Message = "Could not get CAD instance transform." });
                return report;
            }

            double heightFeet = topLevel.Elevation - baseLevel.Elevation;
            if (heightFeet <= 0)
            {
                report.Failed = walls.Count;
                foreach (var w in walls)
                    report.ItemResults.Add(new ImportItemResult { SourceWallId = w.Id, Status = ImportItemStatus.Failed, ReasonCode = "DEGENERATE_HEIGHT", Message = "Wall height must be positive." });
                return report;
            }

            // All validation done; do not start transaction until we've validated each wall length and mapping
            var toCreate = new List<BimJsonWallFeature>();
            foreach (var w in walls)
            {
                double lenMm = Math.Sqrt((w.P2.X - w.P1.X) * (w.P2.X - w.P1.X) + (w.P2.Y - w.P1.Y) * (w.P2.Y - w.P1.Y));
                if (lenMm < MinWallLengthMm)
                {
                    report.Skipped++;
                    report.ItemResults.Add(new ImportItemResult { SourceWallId = w.Id, Status = ImportItemStatus.Skipped, ReasonCode = "DEGENERATE_LINE", Message = $"Length {lenMm:F0} mm < {MinWallLengthMm} mm." });
                    continue;
                }
                var key = Math.Round(w.ThicknessCm, 2);
                if (!mappingByThickness.TryGetValue(key, out ElementId wallTypeId))
                {
                    report.Failed++;
                    report.ItemResults.Add(new ImportItemResult { SourceWallId = w.Id, Status = ImportItemStatus.Failed, ReasonCode = "MISSING_MAPPING", Message = $"No wall type for thickness {w.ThicknessCm} cm." });
                    continue;
                }
                toCreate.Add(w);
            }

            // Now run transaction
            using (var tg = new TransactionGroup(doc, "Create Walls"))
            {
                tg.Start();
                try
                {
                    using (var t = new Transaction(doc, "Create Walls"))
                    {
                        t.Start();
                        foreach (var w in toCreate)
                        {
                            var key = Math.Round(w.ThicknessCm, 2);
                            var wallTypeId = mappingByThickness[key];
                            XYZ p1Feet = new XYZ(
                                UnitService.MmToFeet(w.P1.X),
                                UnitService.MmToFeet(w.P1.Y),
                                0);
                            XYZ p2Feet = new XYZ(
                                UnitService.MmToFeet(w.P2.X),
                                UnitService.MmToFeet(w.P2.Y),
                                0);
                            p1Feet = transform.OfPoint(p1Feet);
                            p2Feet = transform.OfPoint(p2Feet);

                            Line line = Line.CreateBound(p1Feet, p2Feet);
                            Wall wall = Wall.Create(doc, line, wallTypeId, baseLevelId, heightFeet, 0, false, false);
                            if (wall != null)
                            {
                                var topParam = wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE);
                                if (topParam != null && !topParam.IsReadOnly)
                                    topParam.Set(topLevelId);
                                report.Created++;
                                report.ItemResults.Add(new ImportItemResult { SourceWallId = w.Id, Status = ImportItemStatus.Created, CreatedElementIdString = wall.Id.IntegerValue.ToString() });
                            }
                            else
                            {
                                report.Failed++;
                                report.ItemResults.Add(new ImportItemResult { SourceWallId = w.Id, Status = ImportItemStatus.Failed, ReasonCode = "CREATE_FAILED", Message = "Wall.Create returned null." });
                            }
                        }
                        t.Commit();
                    }
                    tg.Assimilate();
                }
                catch (Exception ex)
                {
                    tg.RollBack();
                    report.Failed += toCreate.Count;
                    foreach (var w in toCreate)
                        report.ItemResults.Add(new ImportItemResult { SourceWallId = w.Id, Status = ImportItemStatus.Failed, ReasonCode = "EXCEPTION", Message = ex.Message });
                }
            }

            return report;
        }
    }
}
