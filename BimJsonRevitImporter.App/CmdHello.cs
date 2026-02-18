using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimJsonRevitImporters.UI.Views;

namespace BimJsonRevitImporter.App
{
    [Transaction(TransactionMode.Manual)]
    public class CmdHello : IExternalCommand
    {
        private static RevitEventDispatcher _dispatcher;
        private static ExternalEvent _externalEvent;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (_dispatcher == null)
            {
                var uiApp = commandData.Application;
                _dispatcher = new RevitEventDispatcher();
                var handler = new RevitExternalEventHandler(_dispatcher, uiApp);
                _externalEvent = ExternalEvent.Create(handler);
                _dispatcher.SetExternalEvent(_externalEvent);
            }

            MainImportWindowHelper.ShowOrActivate(_dispatcher, ClearStaticRefs);
            return Result.Succeeded;
        }

        private static void ClearStaticRefs()
        {
            _dispatcher = null;
            _externalEvent = null;
        }
    }
}
