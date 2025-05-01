using Core.Security;
using Data.Interfaces;
using Data.Models;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using System.Text.RegularExpressions;
using FluentFTP;
using Core.Utils;

namespace Core.Services.Transfer
{
    public class FileTransferService : IFileTransferService
    {
        private readonly IRepository<FileTransferTask> _taskRepository;
        private readonly IRepository<ServerCredential> _credentialRepository;
        private readonly IRepository<TransferExecution> _executionRepository;
        private readonly IRepository<TransferredFile> _transferredFileRepository;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<FileTransferService> _logger;

        public FileTransferService(
            IRepository<FileTransferTask> taskRepository,
            IRepository<ServerCredential> credentialRepository,
            IRepository<TransferExecution> executionRepository,
            IRepository<TransferredFile> transferredFileRepository,
            IEncryptionService encryptionService,
            ILogger<FileTransferService> logger)
        {
            _taskRepository = taskRepository;
            _credentialRepository = credentialRepository;
            _executionRepository = executionRepository;
            _transferredFileRepository = transferredFileRepository;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        #region Task Management

        public async Task<FileTransferTask> CreateTaskAsync(FileTransferTask task)
        {
            return await _taskRepository.AddAsync(task);
        }

        public async Task<FileTransferTask> UpdateTaskAsync(FileTransferTask task)
        {
            return await _taskRepository.UpdateAsync(task);
        }

        public async Task<bool> DeleteTaskAsync(int taskId)
        {
            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
                return false;

            await _taskRepository.DeleteAsync(taskId);
            return true;
        }

        public async Task<FileTransferTask?> GetTaskByIdAsync(int taskId)
        {
            return await _taskRepository.GetByIdAsync(taskId);
        }

        public async Task<IEnumerable<FileTransferTask>> GetAllTasksAsync()
        {
            return await _taskRepository.GetAllAsync();
        }

        public async Task<IEnumerable<FileTransferTask>> GetActiveTasksAsync()
        {
            var allTasks = await _taskRepository.GetAllAsync();
            return allTasks.Where(t => t.IsActive);
        }

        #endregion

        #region Execution

        public async Task<TransferExecution> ExecuteTaskAsync(int taskId)
        {
            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
                throw new ArgumentException($"Task with ID {taskId} not found");

            return await ExecuteTaskAsync(task);
        }

        public async Task<TransferExecution> ExecuteTaskAsync(FileTransferTask task)
        {
            var execution = new TransferExecution
            {
                FileTransferTaskId = task.Id,
                StartTime = DateTime.UtcNow,
                Status = "In Progress"
            };

            execution = await _executionRepository.AddAsync(execution);

            try
            {
                var sourceCredential = await _credentialRepository.GetByIdAsync(task.SourceCredentialId);
                var destinationCredential = await _credentialRepository.GetByIdAsync(task.DestinationCredentialId);

                if (sourceCredential == null || destinationCredential == null)
                    throw new ArgumentException("Source or destination credential not found");

                var sourceFiles = await GetFilesFromServerAsync(
                    sourceCredential,
                    task.SourceFolder,
                    task.FilePattern,
                    task.CopySubfolders);

                HashSet<string> processedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var sourceFile in sourceFiles)
                {
                    var result = await TransferFileAsync(
                        execution.Id,
                        sourceCredential,
                        destinationCredential,
                        sourceFile,
                        task.SourceFolder,
                        task.DestinationFolder,
                        task.CreateSubfolders,
                        task.DeleteSourceFolderAfterTransfer);

                    if (result.TransferSuccessful && task.DeleteSourceFolderAfterTransfer)
                    {
                        string parentFolder = Path.GetDirectoryName(sourceFile)?.Replace('\\', '/') ?? string.Empty;
                        if (!string.IsNullOrEmpty(parentFolder))
                        {
                            processedFolders.Add(parentFolder);
                        }
                    }
                }

                if (task.DeleteSourceFolderAfterTransfer)
                {
                    if (sourceCredential.ServerType.ToUpper() == "NETWORK")
                    {
                        string networkPath = "\\\\" + sourceCredential.Host + (task.SourceFolder.StartsWith("/") ? task.SourceFolder : "/" + task.SourceFolder);
                        await DeleteNetworkFoldersRecursivelyAsync(networkPath, processedFolders);
                    }
                    else
                    {
                        await DeleteFoldersRecursivelyAsync(sourceCredential, task.SourceFolder, processedFolders);
                    }
                }

                execution.EndTime = DateTime.UtcNow;
                execution.Status = "Completed";
                await _executionRepository.UpdateAsync(execution);

                return execution;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing task {task.Id}: {ex.Message}");

                execution.EndTime = DateTime.UtcNow;
                execution.Status = "Error";
                execution.ErrorMessage = ex.Message;
                await _executionRepository.UpdateAsync(execution);

                return execution;
            }
        }

        public async Task<bool> CancelExecutionAsync(int executionId)
        {
            var execution = await _executionRepository.GetByIdAsync(executionId);
            if (execution == null || execution.Status != "In Progress")
                return false;

            execution.Status = "Cancelled";
            execution.EndTime = DateTime.UtcNow;
            await _executionRepository.UpdateAsync(execution);

            return true;
        }

        private async Task DeleteFoldersRecursivelyAsync(
            ServerCredential credential,
            string baseFolder,
            HashSet<string> processedFolders)
        {
            baseFolder = baseFolder.Replace('\\', '/').TrimEnd('/');

            var allFolders = processedFolders
                .OrderByDescending(f => f.Count(c => c == '/'))
                .ToList();

            foreach (var folder in allFolders)
            {
                if (folder.Equals(baseFolder, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    _logger.LogInformation($"Deleting folder: {folder}");
                    await DeleteFolderAsync(credential, folder);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Could not delete folder {folder}: {ex.Message}");
                }
            }
        }

        private async Task DeleteFolderAsync(ServerCredential credential, string remotePath)
        {
            switch (credential.ServerType.ToUpper())
            {
                case "FTP":
                    using (var client = CreateFtpClient(credential))
                    {
                        await Task.Run(() => client.Connect());

                        var listing = client.GetListing(remotePath);
                        if (listing.Length == 0) 
                        {
                            client.DeleteDirectory(remotePath);
                        }
                        else
                        {
                            _logger.LogWarning($"Folder not empty, skipping deletion: {remotePath}");
                        }
                    }
                    break;

                case "SFTP":
                    using (var client = CreateSftpClient(credential))
                    {
                        await Task.Run(() => client.Connect());

                        var listing = client.ListDirectory(remotePath).ToList();
                        var nonSpecialEntries = listing.Where(e => e.Name != "." && e.Name != "..").ToList();

                        if (nonSpecialEntries.Count == 0) 
                        {
                            client.DeleteDirectory(remotePath);
                        }
                        else
                        {
                            _logger.LogWarning($"Folder not empty, skipping deletion: {remotePath}");
                        }
                    }
                    break;

                default:
                    throw new NotSupportedException($"Server type {credential.ServerType} is not supported for DeleteFolderAsync");
            }
        }

        #endregion

        #region Execution History

        public async Task<IEnumerable<TransferExecution>> GetTaskExecutionsAsync(int taskId)
        {
            var allExecutions = await _executionRepository.GetAllAsync();
            return allExecutions.Where(e => e.FileTransferTaskId == taskId);
        }

        public async Task<TransferExecution?> GetExecutionByIdAsync(int executionId)
        {
            return await _executionRepository.GetByIdAsync(executionId);
        }

        public async Task<IEnumerable<TransferredFile>> GetExecutionFilesAsync(int executionId)
        {
            var allFiles = await _transferredFileRepository.GetAllAsync();
            return allFiles.Where(f => f.TransferExecutionId == executionId);
        }

        #endregion

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

            if (!string.IsNullOrEmpty(credential.EncryptedPassword) &&
                credential.EncryptedPassword != existingCredential.EncryptedPassword)
            {
                credential.EncryptedPassword = _encryptionService.Encrypt(credential.EncryptedPassword);
            }

            return await _credentialRepository.UpdateAsync(credential);
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

        public async Task<IEnumerable<ServerCredential>> GetAllCredentialsAsync()
        {
            return await _credentialRepository.GetAllAsync();
        }

        #endregion

        #region Testing Connections

        public async Task<bool> TestConnectionAsync(ServerCredential credential,string? folder = "")
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
                        string networkPath = BuildNetworkPath(credential, folder??"");

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

        #endregion

        #region File Operations

        private async Task<IEnumerable<string>> GetFilesFromServerAsync(
            ServerCredential credential,
            string folder,
            string? filePattern,
            bool includeSubfolders = false)
        {
            List<string> files = new List<string>();

            switch (credential.ServerType.ToUpper())
            {
                case "FTP":
                    using (var client = CreateFtpClient(credential))
                    {
                        await Task.Run(() => client.Connect());
                        await GetFtpFilesRecursiveAsync(client, folder, filePattern, includeSubfolders, files);
                    }
                    break;

                case "SFTP":
                    using (var client = CreateSftpClient(credential))
                    {
                        await Task.Run(() => client.Connect());
                        await GetSftpFilesRecursiveAsync(client, folder, filePattern, includeSubfolders, files);
                    }
                    break;

                case "NETWORK":
                    var networkFiles = await GetNetworkFilesAsync(credential, folder, filePattern, includeSubfolders);
                    files.AddRange(networkFiles);
                    break;

                default:
                    throw new NotSupportedException($"Server type {credential.ServerType} is not supported");
            }

            return files;
        }

        private async Task GetFtpFilesRecursiveAsync(
            FtpClient client,
            string folder,
            string? filePattern,
            bool includeSubfolders,
            List<string> files)
        {
            var listing = client.GetListing(folder);

            foreach (var item in listing.Where(i => i.Type == FluentFTP.FtpObjectType.File))
            {
                if (string.IsNullOrEmpty(filePattern) || Regex.IsMatch(item.Name, filePattern))
                {
                    files.Add(Path.Combine(folder, item.Name).Replace('\\', '/'));
                }
            }

            if (includeSubfolders)
            {
                foreach (var item in listing.Where(i => i.Type == FluentFTP.FtpObjectType.Directory))
                {
                    string subFolder = Path.Combine(folder, item.Name).Replace('\\', '/');
                    await GetFtpFilesRecursiveAsync(client, subFolder, filePattern, true, files);
                }
            }
        }

        private async Task GetSftpFilesRecursiveAsync(
            SftpClient client,
            string folder,
            string? filePattern,
            bool includeSubfolders,
            List<string> files)
        {
            var listing = client.ListDirectory(folder);

            foreach (var item in listing.Where(i => i.IsRegularFile))
            {
                if (string.IsNullOrEmpty(filePattern) || Regex.IsMatch(item.Name, filePattern))
                {
                    files.Add(Path.Combine(folder, item.Name).Replace('\\', '/'));
                }
            }

            if (includeSubfolders)
            {
                foreach (var item in listing.Where(i => i.IsDirectory && !i.Name.Equals(".") && !i.Name.Equals("..")))
                {
                    string subFolder = Path.Combine(folder, item.Name).Replace('\\', '/');
                    await GetSftpFilesRecursiveAsync(client, subFolder, filePattern, true, files);
                }
            }
        }

        private async Task<TransferredFile> TransferFileAsync(
            int executionId,
            ServerCredential source,
            ServerCredential destination,
            string sourceFilePath,
            string sourceBaseFolder,
            string destinationBaseFolder,
            bool createSubfolders,
            bool deleteSource)
        {
            string fileName = Path.GetFileName(sourceFilePath);
            string destinationFilePath;

            if (createSubfolders && sourceFilePath.StartsWith(sourceBaseFolder, StringComparison.OrdinalIgnoreCase))
            {
                string relativePath = sourceFilePath.Substring(sourceBaseFolder.Length).TrimStart('/');
                destinationFilePath = Path.Combine(destinationBaseFolder, relativePath).Replace('\\', '/');
            }
            else
            {
                destinationFilePath = Path.Combine(destinationBaseFolder, fileName).Replace('\\', '/');
            }

            var transferredFile = new TransferredFile
            {
                TransferExecutionId = executionId,
                FileName = fileName,
                SourcePath = sourceFilePath,
                DestinationPath = destinationFilePath,
                FileSize = 0, 
                TransferSuccessful = false
            };

            try
            {
                if (source.ServerType.ToUpper() == "NETWORK" && destination.ServerType.ToUpper() == "NETWORK")
                {
                    return await TransferNetworkFileAsync(
                        executionId,
                        source,
                        destination,
                        sourceFilePath,
                        sourceBaseFolder,
                        destinationBaseFolder,
                        createSubfolders,
                        deleteSource);
                }

                string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

                try
                {
                    await DownloadFileAsync(source, sourceFilePath, tempFile);

                    string destinationDir = Path.GetDirectoryName(destinationFilePath)!;
                    await EnsureDirectoryExistsAsync(destination, destinationDir);

                    await UploadFileAsync(destination, tempFile, destinationFilePath);

                    var fileInfo = new FileInfo(tempFile);
                    transferredFile.FileSize = fileInfo.Length;

                    if (deleteSource)
                    {
                        await DeleteFileAsync(source, sourceFilePath);
                    }

                    transferredFile.TransferSuccessful = true;
                }
                finally
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error transferring file {sourceFilePath}: {ex.Message}");
                transferredFile.ErrorMessage = ex.Message;
            }

            return await _transferredFileRepository.AddAsync(transferredFile);
        }

        private async Task DownloadFileAsync(ServerCredential credential, string remotePath, string localPath)
        {
            switch (credential.ServerType.ToUpper())
            {
                case "FTP":
                    using (var client = CreateFtpClient(credential))
                    {
                        await Task.Run(() => client.Connect());
                        client.DownloadFile(localPath, remotePath);
                    }
                    break;

                case "SFTP":
                    using (var client = CreateSftpClient(credential))
                    {
                        await Task.Run(() => client.Connect());
                        using (var localStream = File.Create(localPath))
                        {
                            client.DownloadFile(remotePath, localStream);
                        }
                    }
                    break;

                case "NETWORK":
                    string networkPath = remotePath.Replace('/', '\\');

                    if (!string.IsNullOrEmpty(credential.Username))
                    {
                        string basePath = credential.Host.StartsWith("\\\\") ? credential.Host : $"\\\\{credential.Host}";
                        using (var networkConnection = new NetworkConnection(
                            basePath, credential.Username, _encryptionService.Decrypt(credential.EncryptedPassword)))
                        {
                            File.Copy(networkPath, localPath, true);
                        }
                    }
                    else
                    {
                        File.Copy(networkPath, localPath, true);
                    }
                    break;

                default:
                    throw new NotSupportedException($"Server type {credential.ServerType} is not supported for download");
            }
        }

        private async Task UploadFileAsync(ServerCredential credential, string localPath, string remotePath)
        {
            switch (credential.ServerType.ToUpper())
            {
                case "FTP":
                    using (var client = CreateFtpClient(credential))
                    {
                        await Task.Run(() => client.Connect());
                        client.UploadFile(localPath, remotePath);
                    }
                    break;

                case "SFTP":
                    using (var client = CreateSftpClient(credential))
                    {
                        await Task.Run(() => client.Connect());
                        using (var localStream = File.OpenRead(localPath))
                        {
                            client.UploadFile(localStream, remotePath);
                        }
                    }
                    break;

                case "NETWORK":
                    string networkPath = remotePath.Replace('/', '\\');

                    string directory = Path.GetDirectoryName(networkPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (!string.IsNullOrEmpty(credential.Username))
                    {
                        string basePath = credential.Host.StartsWith("\\\\") ? credential.Host : $"\\\\{credential.Host}";
                        using (var networkConnection = new NetworkConnection(
                            basePath, credential.Username, _encryptionService.Decrypt(credential.EncryptedPassword)))
                        {
                            File.Copy(localPath, networkPath, true);
                        }
                    }
                    else
                    {
                        File.Copy(localPath, networkPath, true);
                    }
                    break;

                default:
                    throw new NotSupportedException($"Server type {credential.ServerType} is not supported for upload");
            }
        }

        private async Task DeleteFileAsync(ServerCredential credential, string remotePath)
        {
            switch (credential.ServerType.ToUpper())
            {
                case "FTP":
                    using (var client = CreateFtpClient(credential))
                    {
                        await Task.Run(() => client.Connect());
                        client.DeleteFile(remotePath);
                    }
                    break;

                case "SFTP":
                    using (var client = CreateSftpClient(credential))
                    {
                        await Task.Run(() => client.Connect());
                        client.DeleteFile(remotePath);
                    }
                    break;

                case "NETWORK":
                    string networkPath = remotePath.Replace('/', '\\');

                    if (!string.IsNullOrEmpty(credential.Username))
                    {
                        string basePath = credential.Host.StartsWith("\\\\") ? credential.Host : $"\\\\{credential.Host}";
                        using (var networkConnection = new NetworkConnection(
                            basePath, credential.Username, _encryptionService.Decrypt(credential.EncryptedPassword)))
                        {
                            if (File.Exists(networkPath))
                            {
                                File.Delete(networkPath);
                            }
                        }
                    }
                    else
                    {
                        if (File.Exists(networkPath))
                        {
                            File.Delete(networkPath);
                        }
                    }
                    break;

                default:
                    throw new NotSupportedException($"Server type {credential.ServerType} is not supported for file deletion");
            }
        }

        private async Task EnsureDirectoryExistsAsync(ServerCredential credential, string remotePath)
        {
            if (string.IsNullOrEmpty(remotePath))
                return;

            switch (credential.ServerType.ToUpper())
            {
                case "FTP":
                    using (var client = CreateFtpClient(credential))
                    {
                        await Task.Run(() => client.Connect());
                        client.CreateDirectory(remotePath, true);
                    }
                    break;

                case "SFTP":
                    using (var client = CreateSftpClient(credential))
                    {
                        await Task.Run(() => client.Connect());

                        string[] segments = remotePath.Split('/');
                        string currentPath = "";

                        for (int i = 0; i < segments.Length; i++)
                        {
                            if (string.IsNullOrEmpty(segments[i]))
                                continue;

                            currentPath += "/" + segments[i];

                            try
                            {
                                if (!client.Exists(currentPath))
                                {
                                    client.CreateDirectory(currentPath);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, $"Error creating directory {currentPath}: {ex.Message}");
                            }
                        }
                    }
                    break;

                case "NETWORK":
                    string networkPath = remotePath.Replace('/', '\\');

                    if (!string.IsNullOrEmpty(credential.Username))
                    {
                        string basePath = credential.Host.StartsWith("\\\\") ? credential.Host : $"\\\\{credential.Host}";
                        using (var networkConnection = new NetworkConnection(
                            basePath, credential.Username, _encryptionService.Decrypt(credential.EncryptedPassword)))
                        {
                            if (!Directory.Exists(networkPath))
                            {
                                Directory.CreateDirectory(networkPath);
                            }
                        }
                    }
                    else
                    {
                        if (!Directory.Exists(networkPath))
                        {
                            Directory.CreateDirectory(networkPath);
                        }
                    }
                    break;

                default:
                    throw new NotSupportedException($"Server type {credential.ServerType} is not supported for directory creation");
            }
        }

        #endregion

        #region Client Creation

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

        #endregion

        #region Network Share Operations

        private string BuildNetworkPath(ServerCredential credential, string folder)
        {
            if (credential.Host.StartsWith("\\\\"))
            {
                return Path.Combine(credential.Host, folder).Replace('/', '\\');
            }
            return $"\\\\{credential.Host}\\{folder.TrimStart('/', '\\')}";
        }

        private async Task<IEnumerable<string>> GetNetworkFilesAsync(
            ServerCredential credential,
            string folder,
            string? filePattern,
            bool includeSubfolders = false)
        {
            List<string> files = new List<string>();

            try
            {
                string networkPath = BuildNetworkPath(credential, folder);

                if (!string.IsNullOrEmpty(credential.Username))
                {
                    using (var networkConnection = new NetworkConnection(networkPath, credential.Username,
                        _encryptionService.Decrypt(credential.EncryptedPassword)))
                    {
                        files = await ScanNetworkFolderAsync(networkPath, filePattern, includeSubfolders);
                    }
                }
                else
                {
                    files = await ScanNetworkFolderAsync(networkPath, filePattern, includeSubfolders);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error listing files in network folder {folder} for server {credential.Host}: {ex.Message}");
                throw;
            }

            return files;
        }

        private async Task<List<string>> ScanNetworkFolderAsync(
            string networkPath,
            string? filePattern,
            bool includeSubfolders)
        {
            List<string> files = new List<string>();

            try
            {
                foreach (var file in Directory.GetFiles(networkPath))
                {
                    string fileName = Path.GetFileName(file);
                    if (string.IsNullOrEmpty(filePattern) || Regex.IsMatch(fileName, filePattern))
                    {
                        files.Add(file.Replace('\\', '/'));
                    }
                }

                if (includeSubfolders)
                {
                    foreach (var subDir in Directory.GetDirectories(networkPath))
                    {
                        var subDirFiles = await ScanNetworkFolderAsync(subDir, filePattern, true);
                        files.AddRange(subDirFiles);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error scanning network folder {networkPath}: {ex.Message}");
                throw;
            }

            return files;
        }

        private async Task<TransferredFile> TransferNetworkFileAsync(
            int executionId,
            ServerCredential source,
            ServerCredential destination,
            string sourceFilePath,
            string sourceFolderPath,
            string destinationFolderPath,
            bool createSubfolders,
            bool deleteSource)
        {
            string fileName = Path.GetFileName(sourceFilePath);
            string destinationFilePath;

            string sourceNetworkPath = sourceFilePath.Replace('/', '\\');
            string destBasePath = BuildNetworkPath(destination, destinationFolderPath);

            sourceFilePath = sourceFilePath.Substring(source.Host.Length + 2);

            if (createSubfolders && sourceFilePath.StartsWith(sourceFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                string relativePath = sourceFilePath.Substring(sourceFolderPath.Length).TrimStart('\\', '/');
                string relativeDirectory = Path.GetDirectoryName(relativePath)!;

                if (!string.IsNullOrEmpty(relativeDirectory))
                {
                    destinationFilePath = Path.Combine(destBasePath, relativeDirectory, fileName).Replace('/', '\\');
                }
                else
                {
                    destinationFilePath = Path.Combine(destBasePath, fileName).Replace('/', '\\');
                }
            }
            else
            {
                destinationFilePath = Path.Combine(destBasePath, fileName).Replace('/', '\\');
            }

            var transferredFile = new TransferredFile
            {
                TransferExecutionId = executionId,
                FileName = fileName,
                SourcePath = sourceFilePath,
                DestinationPath = destinationFilePath,
                FileSize = 0,
                TransferSuccessful = false
            };

            try
            {
                NetworkConnection sourceConnection = null;
                NetworkConnection destConnection = null;

                try
                {
                    if (!string.IsNullOrEmpty(source.Username))
                    {
                        string sourcePath = source.Host.StartsWith("\\\\") ? source.Host : $"\\\\{source.Host}";
                        sourceConnection = new NetworkConnection(sourcePath, source.Username, _encryptionService.Decrypt(source.EncryptedPassword));
                    }

                    if (!string.IsNullOrEmpty(destination.Username))
                    {
                        string destPath = destination.Host.StartsWith("\\\\") ? destination.Host : $"\\\\{destination.Host}";
                        destConnection = new NetworkConnection(destPath, destination.Username, _encryptionService.Decrypt(destination.EncryptedPassword));
                    }

                    string destinationDir = Path.GetDirectoryName(destinationFilePath)!;
                    if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    var fileInfo = new FileInfo(sourceNetworkPath);
                    transferredFile.FileSize = fileInfo.Length;

                    await Task.Run(() => File.Copy(sourceNetworkPath, destinationFilePath, true));

                    if (deleteSource)
                    {
                        await Task.Run(() => File.Delete(sourceNetworkPath));
                    }

                    transferredFile.TransferSuccessful = true;
                }
                finally
                {
                    sourceConnection?.Dispose();
                    destConnection?.Dispose();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error transferring network file {sourceFilePath}: {ex.Message}");
                transferredFile.ErrorMessage = ex.Message;
            }

            return await _transferredFileRepository.AddAsync(transferredFile);
        }



        private async Task DeleteNetworkFolderAsync(string folderPath)
        {
            try
            {
                string normalizedPath = folderPath.Replace('/', '\\');
                if (Directory.Exists(normalizedPath) &&
                    !Directory.GetFiles(normalizedPath).Any() &&
                    !Directory.GetDirectories(normalizedPath).Any())
                {
                    await Task.Run(() => Directory.Delete(normalizedPath));
                    _logger.LogInformation($"Deleted empty folder: {folderPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to delete folder {folderPath}: {ex.Message}");
            }
        }

        private async Task DeleteNetworkFoldersRecursivelyAsync(string baseFolder, HashSet<string> processedFolders)
        {
            baseFolder = baseFolder.Replace('\\', '/').TrimEnd('/');

            var allFolders = processedFolders
                .OrderByDescending(f => f.Count(c => c == '/'))
                .ToList();

            foreach (var folder in allFolders)
            {
                if (folder.Equals(baseFolder, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    _logger.LogInformation($"Deleting folder: {folder}");
                    await DeleteNetworkFolderAsync(folder);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Could not delete folder {folder}: {ex.Message}");
                }
            }
        }
        #endregion
    }
}
