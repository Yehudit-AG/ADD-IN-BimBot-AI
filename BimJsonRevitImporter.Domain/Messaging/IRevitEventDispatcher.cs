using System.Threading.Tasks;

namespace BimJsonRevitImporter.Domain.Messaging
{
    public interface IRevitEventDispatcher
    {
        Task<RevitResponse> SendAsync(RevitRequest request);
    }
}
