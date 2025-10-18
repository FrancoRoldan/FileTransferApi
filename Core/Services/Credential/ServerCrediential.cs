using Core.Security;
using Data.Dtos.FileTransfer;
using Data.Interfaces;
using Data.Models;
using Mapster;
using Microsoft.Extensions.Logging;

namespace Core.Services.Credential
{
    public class ServerCrediential:IServerCredential
    {
        private readonly IServerCredentialRepository _credentialRepository;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<ServerCrediential> _logger;

        public ServerCrediential(
            IServerCredentialRepository credentialRepository,
            IEncryptionService encryptionService,
            ILogger<ServerCrediential> logger)
        {
            _credentialRepository = credentialRepository;
            _encryptionService = encryptionService;
            _logger = logger;
        }
        #region Server Credential Management

        public async Task<ServerCredential> CreateCredentialAsync(ServerCredential credential)
        {
            if (!string.IsNullOrEmpty(credential.EncryptedPassword))
            {
                credential.EncryptedPassword = _encryptionService.Encrypt(credential.EncryptedPassword);
            }

            return await _credentialRepository.AddAsync(credential);
        }

        public async Task<ServerCredential> UpdateCredentialAsync(ServerCredential credential)
        {
            var existingCredential = await _credentialRepository.GetByIdAsync(credential.Id);

            if (existingCredential == null)
                throw new InvalidOperationException("Credential not found");

            if (!string.IsNullOrEmpty(credential.EncryptedPassword) &&
                credential.EncryptedPassword != existingCredential.EncryptedPassword)
            {
                credential.EncryptedPassword = _encryptionService.Encrypt(credential.EncryptedPassword);
            }

            credential.Adapt(existingCredential);

            return await _credentialRepository.UpdateAsync(existingCredential);
        }

        public async Task<bool> DeleteCredentialAsync(int credentialId)
        {
            var credential = await _credentialRepository.GetByIdAsync(credentialId);
            if (credential == null)
                return false;

            await _credentialRepository.DeleteAsync(credentialId);
            return true;
        }

        public async Task<ServerCredential?> GetCredentialByIdAsync(int credentialId)
        {
            return await _credentialRepository.GetByIdAsync(credentialId);
        }

        public async Task<PaginatedResponseDto<ServerCredential>> GetPaginatedCredentialsAsync(int pageIndex, int pageSize, string searchTerm = "")
        {
            var totalCount = await _credentialRepository.CountAsync(searchTerm);
            var tasks = await _credentialRepository.GetPaginatedAsync(pageIndex, pageSize, searchTerm);

            return new PaginatedResponseDto<ServerCredential>
            {
                Items = tasks.OrderBy(t => t.Id).Adapt<List<ServerCredential>>(),
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        public async Task<IEnumerable<ServerCredential>> GetAllCredentialsAsync()
        {
            var credentials = await _credentialRepository.GetAllAsync();
            return credentials.OrderBy(x => x.Id); ;
        }

        #endregion
    }
}
