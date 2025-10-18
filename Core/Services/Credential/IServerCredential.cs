using Data.Dtos.FileTransfer;
using Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Services.Credential
{
    public interface IServerCredential
    {
        Task<ServerCredential> CreateCredentialAsync(ServerCredential credential);
        Task<ServerCredential> UpdateCredentialAsync(ServerCredential credential);
        Task<bool> DeleteCredentialAsync(int credentialId);
        Task<ServerCredential?> GetCredentialByIdAsync(int credentialId);
        Task<PaginatedResponseDto<ServerCredential>> GetPaginatedCredentialsAsync(int pageIndex, int pageSize, string searchTerm = "");
        Task<IEnumerable<ServerCredential>> GetAllCredentialsAsync();
    }
}
