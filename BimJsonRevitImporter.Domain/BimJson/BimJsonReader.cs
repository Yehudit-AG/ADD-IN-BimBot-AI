using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BimJsonRevitImporter.Domain.BimJson
{
    /// <summary>
    /// Reads BIMJSON file (type "BIMJSON" or "FeatureCollection") and extracts wall features for v1.
    /// Supports coordinates as [x,y] or [x,y,z]; thickness from properties, properties.computed.thickness_cm, or properties.revit.mapping.thickness_cm.
    /// ReadWalls uses streaming for files of any size (including &gt; 2 GB); no in-memory limit for file size.
    /// </summary>
    public static class BimJsonReader
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        /// <summary>
        /// Reads walls from a BIMJSON file using streaming. Supports files larger than 2 GB
        /// (no entire-file string in memory; each feature is parsed one at a time).
        /// </summary>
        public static List<BimJsonWallFeature> ReadWalls(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return new List<BimJsonWallFeature>();

            var walls = new List<BimJsonWallFeature>();
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan))
                using (var textReader = new StreamReader(stream, Encoding.UTF8, true, 1024 * 1024))
                using (var jsonReader = new JsonTextReader(textReader))
                {
                    if (!jsonReader.Read() || jsonReader.TokenType != JsonToken.StartObject)
                        return walls;

                    string rootType = null;
                    while (jsonReader.Read())
                    {
                        if (jsonReader.TokenType == JsonToken.EndObject)
                            break;
                        if (jsonReader.TokenType != JsonToken.PropertyName)
                            continue;

                        var propName = (string)jsonReader.Value;
                        if (propName == "type")
                        {
                            jsonReader.Read();
                            rootType = (string)jsonReader.Value;
                        }
                        else if (propName == "features")
                        {
                            jsonReader.Read();
                            if (jsonReader.TokenType != JsonToken.StartArray)
                                continue;
                            while (jsonReader.Read())
                            {
                                if (jsonReader.TokenType == JsonToken.EndArray)
                                    break;
                                if (jsonReader.TokenType == JsonToken.StartObject)
                                {
                                    var feature = JObject.Load(jsonReader);
                                    var wall = ParseFeatureToWall(feature);
                                    if (wall != null)
                                        walls.Add(wall);
                                }
                            }
                            break;
                        }
                        else
                        {
                            jsonReader.Read();
                            jsonReader.Skip();
                        }
                    }

                    if (rootType == null || (!"FeatureCollection".Equals(rootType) && !"BIMJSON".Equals(rootType)))
                        return new List<BimJsonWallFeature>();
                }
            }
            catch
            {
                // Return partial or empty list on parse/IO error
            }

            return walls;
        }

        /// <summary>
        /// Parses a single GeoJSON feature (JObject) into a BimJsonWallFeature if geometry is LineString.
        /// </summary>
        private static BimJsonWallFeature ParseFeatureToWall(JObject feature)
        {
            if (feature == null) return null;
            var geom = feature["geometry"] as JObject;
            if (geom == null || !"LineString".Equals(geom["type"]?.ToString())) return null;

            var coords = geom["coordinates"] as JArray;
            if (coords == null || coords.Count < 2) return null;

            var p0 = coords[0] as JArray;
            var p1 = coords[1] as JArray;
            if (p0 == null || p1 == null || p0.Count < 2 || p1.Count < 2) return null;

            double x0 = ToDoubleJ(p0[0]), y0 = ToDoubleJ(p0[1]);
            double x1 = ToDoubleJ(p1[0]), y1 = ToDoubleJ(p1[1]);

            var props = feature["properties"] as JObject;
            double thicknessCm = 0;
            string id = feature["id"]?.ToString() ?? props?["id"]?.ToString() ?? Guid.NewGuid().ToString("N");
            if (props != null)
                thicknessCm = GetThicknessFromJObject(props);

            return new BimJsonWallFeature
            {
                Id = id ?? Guid.NewGuid().ToString("N"),
                ThicknessCm = thicknessCm,
                P1 = new PointMm { X = x0, Y = y0 },
                P2 = new PointMm { X = x1, Y = y1 }
            };
        }

        private static double ToDoubleJ(JToken t)
        {
            if (t == null) return 0;
            if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer)
                return t.Value<double>();
            double v;
            return double.TryParse(t.ToString(), out v) ? v : 0;
        }

        private static double GetThicknessFromJObject(JObject props)
        {
            if (props == null) return 0;
            if (props["thicknessCm"] != null) return ToDoubleJ(props["thicknessCm"]);
            if (props["ThicknessCm"] != null) return ToDoubleJ(props["ThicknessCm"]);
            if (props["thickness_cm"] != null) return ToDoubleJ(props["thickness_cm"]);
            var computed = props["computed"] as JObject;
            if (computed?["thickness_cm"] != null) return ToDoubleJ(computed["thickness_cm"]);
            var revit = props["revit"] as JObject;
            var mapping = revit?["mapping"] as JObject;
            if (mapping?["thickness_cm"] != null) return ToDoubleJ(mapping["thickness_cm"]);
            if (props["thicknessMm"] != null) return ToDoubleJ(props["thicknessMm"]) / 10.0;
            if (props["ThicknessMm"] != null) return ToDoubleJ(props["ThicknessMm"]) / 10.0;
            if (props["thickness_mm"] != null) return ToDoubleJ(props["thickness_mm"]) / 10.0;
            if (props["thickness"] != null) return ToDoubleJ(props["thickness"]) / 10.0;
            return 0;
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
