using Core.Security;
using Core.Services.ConnectionTesting;
using Core.Utils;
using Data.Interfaces;
using Data.Models;
using FluentFTP;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using System.IO;
using System.Threading.Tasks;

namespace Core.Services.ConnectionTesting
{
    public class ConnectionTestingService : IConnectionTestingService
    {
        private readonly IServerCredentialRepository _credentialRepository;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<ConnectionTestingService> _logger;

        public ConnectionTestingService(
            IServerCredentialRepository credentialRepository,
            IEncryptionService encryptionService,
            ILogger<ConnectionTestingService> logger)
        {
            _credentialRepository = credentialRepository;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        public async Task<bool> TestConnectionAsync(ServerCredential credential, string? folder = "")
        {
            try
            {
                switch (credential.ServerType.ToUpper())
                {
                    case "FTP":
                        using (var client = CreateFtpClient(credential))
                        {
                            await Task.Run(() => client.Connect());
                            return client.IsConnected;
                        }

                    case "SFTP":
                        using (var client = CreateSftpClient(credential))
                        {
                            await Task.Run(() => client.Connect());
                            return client.IsConnected;
                        }

                    case "NETWORK":
                        string networkPath = BuildNetworkPath(credential, folder ?? "");

                        if (!string.IsNullOrEmpty(credential.Username))
                        {
                            using (var networkConnection = new NetworkConnection(networkPath, credential.Username,
                                _encryptionService.Decrypt(credential.EncryptedPassword)))
                            {
                                return Directory.Exists(networkPath);
                            }
                        }
                        else
                        {
                            return Directory.Exists(networkPath);
                        }

                    default:
                        throw new NotSupportedException($"Server type {credential.ServerType} is not supported");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error testing connection to {credential.Host}: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> TestTaskConnectionsAsync(FileTransferTask task)
        {
            var sourceCredential = await _credentialRepository.GetByIdAsync(task.SourceCredentialId);
            var destinationCredential = await _credentialRepository.GetByIdAsync(task.DestinationCredentialId);

            if (sourceCredential == null || destinationCredential == null)
                return false;
            var sourceResult = false;
            var destinationResult = false;

            if (sourceCredential.ServerType == "NETWORK" && destinationCredential.ServerType == "NETWORK")
            {
                sourceResult = await TestConnectionAsync(sourceCredential, task.SourceFolder);
                destinationResult = await TestConnectionAsync(destinationCredential, task.DestinationFolder);

                return sourceResult && destinationResult;
            }

            sourceResult = await TestConnectionAsync(sourceCredential);
            destinationResult = await TestConnectionAsync(destinationCredential);

            return sourceResult && destinationResult;
        }

        private FtpClient CreateFtpClient(ServerCredential credential)
        {
            string password = _encryptionService.Decrypt(credential.EncryptedPassword);

            return new FtpClient(
                credential.Host,
                credential.Username,
                password,
                credential.Port);
        }

        private SftpClient CreateSftpClient(ServerCredential credential)
        {
            if (!string.IsNullOrEmpty(credential.PrivateKeyPath))
            {
                var keyFile = new PrivateKeyFile(credential.PrivateKeyPath);
                return new SftpClient(
                    credential.Host,
                    credential.Port,
                    credential.Username,
                    keyFile);
            }
            else
            {
                string password = _encryptionService.Decrypt(credential.EncryptedPassword);
                return new SftpClient(
                    credential.Host,
                    credential.Port,
                    credential.Username,
                    password);
            }
        }

        private string BuildNetworkPath(ServerCredential credential, string folder)
        {
            if (credential.Host.StartsWith("\\\\"))
            {
                return Path.Combine(credential.Host, folder).Replace('/', '\\');
            }
            return $"\\\\{credential.Host}\\{folder.TrimStart('/', '\\')}";
        }
    }
}
