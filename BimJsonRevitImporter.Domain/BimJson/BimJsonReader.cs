using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace BimJsonRevitImporter.Domain.BimJson
{
    /// <summary>
    /// Reads BIMJSON file (type "BIMJSON" or "FeatureCollection") and extracts wall features for v1.
    /// Supports coordinates as [x,y] or [x,y,z]; thickness from properties, properties.computed.thickness_cm, or properties.revit.mapping.thickness_cm.
    /// </summary>
    public static class BimJsonReader
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static List<BimJsonWallFeature> ReadWalls(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return new List<BimJsonWallFeature>();

            string json = File.ReadAllText(filePath);
            return ParseWallsFromJson(json);
        }

        public static List<BimJsonWallFeature> ParseWallsFromJson(string json)
        {
            var walls = new List<BimJsonWallFeature>();
            try
            {
                var root = Serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (root == null) return walls;
                var rootType = Convert.ToString(root["type"]);
                if (!"FeatureCollection".Equals(rootType) && !"BIMJSON".Equals(rootType))
                    return walls;

                var featuresRaw = root["features"];
                var featuresList = featuresRaw as IList;
                if (featuresList == null) return walls;

                foreach (var f in featuresList)
                {
                    var feature = f as Dictionary<string, object>;
                    if (feature == null) continue;

                    var geom = feature["geometry"] as Dictionary<string, object>;
                    if (geom == null) continue;
                    if (!"LineString".Equals(Convert.ToString(geom["type"])))
                        continue;

                    var coords = geom["coordinates"] as IList;
                    if (coords == null || coords.Count < 2) continue;

                    var p0 = coords[0] as IList;
                    var p1 = coords[1] as IList;
                    if (p0 == null || p1 == null || p0.Count < 2 || p1.Count < 2) continue;

                    double x0 = ToDouble(p0[0]), y0 = ToDouble(p0[1]);
                    double x1 = ToDouble(p1[0]), y1 = ToDouble(p1[1]);

                    var props = feature["properties"] as Dictionary<string, object>;
                    double thicknessCm = 0;
                    string id = feature.ContainsKey("id") ? Convert.ToString(feature["id"]) : Guid.NewGuid().ToString("N");
                    if (props != null)
                    {
                        thicknessCm = GetThicknessFromProperties(props);
                        if (props.ContainsKey("id")) id = Convert.ToString(props["id"]);
                    }

                    walls.Add(new BimJsonWallFeature
                    {
                        Id = id ?? Guid.NewGuid().ToString("N"),
                        ThicknessCm = thicknessCm,
                        P1 = new PointMm { X = x0, Y = y0 },
                        P2 = new PointMm { X = x1, Y = y1 }
                    });
                }
            }
            catch
            {
                // Return partial or empty list on parse error
            }

            return walls;
        }

        private static double ToDouble(object o)
        {
            if (o == null) return 0;
            if (o is double d) return d;
            if (o is int i) return i;
            if (o is long l) return l;
            if (o is float f) return f;
            double v;
            double.TryParse(Convert.ToString(o), out v);
            return v;
        }

        private static double GetThicknessFromProperties(Dictionary<string, object> props)
        {
            if (props.ContainsKey("thicknessCm")) return ToDouble(props["thicknessCm"]);
            if (props.ContainsKey("ThicknessCm")) return ToDouble(props["ThicknessCm"]);
            if (props.ContainsKey("thickness_cm")) return ToDouble(props["thickness_cm"]);
            var computed = props.ContainsKey("computed") ? props["computed"] as Dictionary<string, object> : null;
            if (computed != null && computed.ContainsKey("thickness_cm")) return ToDouble(computed["thickness_cm"]);
            var revit = props.ContainsKey("revit") ? props["revit"] as Dictionary<string, object> : null;
            if (revit != null)
            {
                var mapping = revit.ContainsKey("mapping") ? revit["mapping"] as Dictionary<string, object> : null;
                if (mapping != null && mapping.ContainsKey("thickness_cm")) return ToDouble(mapping["thickness_cm"]);
            }
            if (props.ContainsKey("thicknessMm")) return ToDouble(props["thicknessMm"]) / 10.0;
            if (props.ContainsKey("ThicknessMm")) return ToDouble(props["ThicknessMm"]) / 10.0;
            if (props.ContainsKey("thickness_mm")) return ToDouble(props["thickness_mm"]) / 10.0;
            if (props.ContainsKey("thickness")) return ToDouble(props["thickness"]) / 10.0;
            return 0;
        }

        public static List<WallGroup> GroupWallsByThickness(List<BimJsonWallFeature> walls)
        {
            var dict = new Dictionary<double, int>();
            foreach (var w in walls)
            {
                var key = Math.Round(w.ThicknessCm, 2);
                if (!dict.ContainsKey(key)) dict[key] = 0;
                dict[key]++;
            }
            var list = new List<WallGroup>();
            foreach (var kv in dict)
                list.Add(new WallGroup { ThicknessCm = kv.Key, Count = kv.Value });
            list.Sort((a, b) => a.ThicknessCm.CompareTo(b.ThicknessCm));
            return list;
        }
    }
}
