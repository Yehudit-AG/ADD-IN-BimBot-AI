using System;
using System.Reflection;
using Autodesk.Revit.UI;

namespace BimJsonRevitImporter.App
{
    public class RevitApp : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            const string tabName = "BimBot";
            const string panelName = "BIMJSON";

            try { app.CreateRibbonTab(tabName); } catch { /* tab may already exist */ }

            var panel = app.CreateRibbonPanel(tabName, panelName);

            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string cmdClass = typeof(CmdHello).FullName;

            var btn = new PushButtonData(
                "BimJsonHello",
                "Hello\nImporter",
                assemblyPath,
                cmdClass
            );

            panel.AddItem(btn);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
    }
}
