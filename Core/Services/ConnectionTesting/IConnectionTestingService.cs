
using Data.Models;
using System.Threading.Tasks;

namespace Core.Services.ConnectionTesting
{
    public interface IConnectionTestingService
    {
        Task<bool> TestConnectionAsync(ServerCredential credential, string? folder = "");
        Task<bool> TestTaskConnectionsAsync(FileTransferTask task);
    }
}
