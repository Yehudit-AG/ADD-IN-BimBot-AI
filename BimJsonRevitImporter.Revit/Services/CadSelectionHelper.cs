using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace BimJsonRevitImporter.Revit.Services
{
    public static class CadSelectionHelper
    {
        public static ElementId PickCadInstance(UIApplication uiApp)
        {
            if (uiApp?.ActiveUIDocument == null) return null;
            var uidoc = uiApp.ActiveUIDocument;
            var doc = uidoc.Document;

            try
            {
                var pickRef = uidoc.Selection.PickObject(
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    new ImportInstanceFilter(doc),
                    "Pick a CAD import instance");
                return pickRef?.ElementId;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }

        private class ImportInstanceFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
        {
            private readonly Document _doc;

            public ImportInstanceFilter(Document doc) { _doc = doc; }

            public bool AllowElement(Element elem)
            {
                return elem is ImportInstance || elem is RevitLinkInstance;
            }

            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
