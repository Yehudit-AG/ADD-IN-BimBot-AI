using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using BimJsonRevitImporter.Domain.Messaging;

namespace BimJsonRevitImporter.App
{
    public class RevitEventDispatcher : IRevitEventDispatcher
    {
        private readonly object _lock = new object();
        private RevitRequest _currentRequest;
        private readonly Dictionary<Guid, TaskCompletionSource<RevitResponse>> _pending = new Dictionary<Guid, TaskCompletionSource<RevitResponse>>();
        private ExternalEvent _externalEvent;

        public void SetExternalEvent(ExternalEvent externalEvent)
        {
            _externalEvent = externalEvent;
        }

        public RevitRequest GetAndClearCurrentRequest()
        {
            lock (_lock)
            {
                var req = _currentRequest;
                _currentRequest = null;
                return req;
            }
        }

        public void Complete(Guid correlationId, RevitResponse response)
        {
            TaskCompletionSource<RevitResponse> tcs;
            lock (_lock)
            {
                if (_pending.TryGetValue(correlationId, out tcs))
                    _pending.Remove(correlationId);
            }
            tcs?.TrySetResult(response);
        }

        public void SetException(Guid correlationId, Exception ex)
        {
            Complete(correlationId, new RevitResponse
            {
                CorrelationId = correlationId,
                Success = false,
                Diagnostics = new List<Diagnostic>
                {
                    new Diagnostic { Code = "EXCEPTION", Message = ex.ToString(), Severity = DiagnosticSeverity.Error }
                }
            });
        }

        public Task<RevitResponse> SendAsync(RevitRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (request.CorrelationId == Guid.Empty)
                request.CorrelationId = Guid.NewGuid();

            var tcs = new TaskCompletionSource<RevitResponse>();
            lock (_lock)
            {
                _currentRequest = request;
                _pending[request.CorrelationId] = tcs;
            }

            _externalEvent?.Raise();
            return tcs.Task;
        }
    }
}
