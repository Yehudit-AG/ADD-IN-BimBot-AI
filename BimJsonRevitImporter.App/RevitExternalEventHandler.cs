using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using BimJsonRevitImporter.Domain.Messaging;

namespace BimJsonRevitImporter.App
{
    public class RevitExternalEventHandler : IExternalEventHandler
    {
        private readonly RevitEventDispatcher _dispatcher;
        private readonly UIApplication _uiApplication;

        /// <summary>
        /// Handler receives UIApplication when created (in ExternalCommand). Must not store it statically.
        /// </summary>
        public RevitExternalEventHandler(RevitEventDispatcher dispatcher, UIApplication uiApplication)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _uiApplication = uiApplication;
        }

        public void Execute(UIApplication app)
        {
            var request = _dispatcher.GetAndClearCurrentRequest();
            if (request == null) return;

            RevitResponse response;
            try
            {
                var uiApp = _uiApplication ?? app;
                var doc = uiApp?.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    response = new RevitResponse
                    {
                        CorrelationId = request.CorrelationId,
                        Success = false,
                        Diagnostics = new List<Diagnostic>
                        {
                            new Diagnostic { Code = "NO_DOCUMENT", Message = "No active document.", Severity = DiagnosticSeverity.Error }
                        }
                    };
                }
                else
                {
                    response = HandleRequest(doc, uiApp, request);
                }
            }
            catch (Exception ex)
            {
                _dispatcher.SetException(request.CorrelationId, ex);
                return;
            }

            response.CorrelationId = request.CorrelationId;
            _dispatcher.Complete(request.CorrelationId, response);
        }

        public string GetName() => "BimJsonRevitImporter";

        private RevitResponse HandleRequest(Document doc, UIApplication uiApp, RevitRequest request)
        {
            switch (request.Type)
            {
                case RevitRequestType.GetSelectableData:
                    return HandleGetSelectableData(doc);
                case RevitRequestType.PickCadInstance:
                    return HandlePickCadInstance(uiApp);
                case RevitRequestType.ImportWalls:
                    return HandleImportWalls(doc, request.Payload as ImportWallsRequestPayload);
                default:
                    return new RevitResponse
                    {
                        Success = false,
                        Diagnostics = new List<Diagnostic>
                        {
                            new Diagnostic { Code = "UNKNOWN_REQUEST", Message = "Unknown request type.", Severity = DiagnosticSeverity.Error }
                        }
                    };
            }
        }

        private RevitResponse HandleGetSelectableData(Document doc)
        {
            var service = new BimJsonRevitImporter.Revit.Services.RevitQueryService();
            var payload = new GetSelectableDataPayload
            {
                Levels = service.GetLevels(doc),
                WallTypes = service.GetWallTypes(doc),
                CadInstances = service.GetCadImportInstances(doc)
            };
            return new RevitResponse { Success = true, Payload = payload };
        }

        private RevitResponse HandlePickCadInstance(UIApplication uiApp)
        {
            var cadId = BimJsonRevitImporter.Revit.Services.CadSelectionHelper.PickCadInstance(uiApp);
            if (cadId == null || cadId == ElementId.InvalidElementId)
                return new RevitResponse
                {
                    Success = false,
                    Diagnostics = new List<Diagnostic>
                    {
                        new Diagnostic { Code = "CANCELLED", Message = "Selection cancelled or invalid.", Severity = DiagnosticSeverity.Warning }
                    }
                };
            var doc = uiApp.ActiveUIDocument.Document;
            var el = doc.GetElement(cadId);
            var name = el?.Name ?? "CAD";
            var uniqueId = el?.UniqueId ?? "";
            var cad = new CadInstanceInfo { ElementIdString = cadId.IntegerValue.ToString(), Name = name, UniqueId = uniqueId };
            return new RevitResponse { Success = true, Payload = cad };
        }

        private RevitResponse HandleImportWalls(Document doc, ImportWallsRequestPayload payload)
        {
            if (payload == null)
                return new RevitResponse { Success = false, Diagnostics = new List<Diagnostic> { new Diagnostic { Code = "NO_PAYLOAD", Message = "Import payload is null.", Severity = DiagnosticSeverity.Error } } };
            var service = new BimJsonRevitImporter.Revit.Services.WallImportService();
            var report = service.ImportWalls(doc, payload);
            return new RevitResponse { Success = true, Payload = report };
        }
    }
}
