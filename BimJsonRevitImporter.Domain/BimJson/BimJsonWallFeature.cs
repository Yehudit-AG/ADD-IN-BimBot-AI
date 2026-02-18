namespace BimJsonRevitImporter.Domain.BimJson
{
    public class BimJsonWallFeature
    {
        public string Id { get; set; }
        public double ThicknessCm { get; set; }
        public PointMm P1 { get; set; }
        public PointMm P2 { get; set; }
    }
}
