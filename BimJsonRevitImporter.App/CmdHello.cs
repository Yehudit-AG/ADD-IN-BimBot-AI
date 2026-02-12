using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimJsonRevitImporter.App
{
    [Transaction(TransactionMode.Manual)]
    public class CmdHello : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            TaskDialog.Show("BimJsonRevitImporter", "Loaded OK");
            return Result.Succeeded;
        }
    }
}
