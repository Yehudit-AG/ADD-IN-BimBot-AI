using Autodesk.Revit.DB;

namespace BimJsonRevitImporter.Revit.Services
{
    /// <summary>
    /// Converts units for Revit 2023 API (UnitTypeId.Millimeters, not deprecated DisplayUnitType).
    /// </summary>
    public static class UnitService
    {
        /// <summary>Convert millimeters to Revit internal units (feet). Uses UnitTypeId.Millimeters (Revit 2023).</summary>
        public static double MmToFeet(double mm)
        {
            return UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
        }
    }
}
