using Core.Security;
using Data.Interfaces;
using Data.Models;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using System.Text.RegularExpressions;
using FluentFTP;

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
            // Create execution record
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

                // Get source files
                var sourceFiles = await GetFilesFromServerAsync(
                    sourceCredential,
                    task.SourceFolder,
                    task.FilePattern,
                    task.CopySubfolders);

                HashSet<string> processedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Transfer each file
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
                        task.DeleteSourceAfterTransfer);

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
                    await DeleteFoldersRecursivelyAsync(sourceCredential, task.SourceFolder, processedFolders);
                }

                // Update execution record
                execution.EndTime = DateTime.UtcNow;
                execution.Status = "Completed";
                await _executionRepository.UpdateAsync(execution);

                return execution;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing task {task.Id}: {ex.Message}");

                // Update execution record with error
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
                // Skip the base folder
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

                        // Check if the folder exists and is empty
                        var listing = client.GetListing(remotePath);
                        if (listing.Length == 0) // If empty, delete it
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

                        // Check if the folder exists and is empty
                        var listing = client.ListDirectory(remotePath).ToList();
                        // SFTP always returns . and .. entries
                        var nonSpecialEntries = listing.Where(e => e.Name != "." && e.Name != "..").ToList();

                        if (nonSpecialEntries.Count == 0) // If empty, delete it
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
                    throw new NotSupportedException($"Server type {credential.ServerType} is not supported");
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
            // Encrypt password before saving
            if (!string.IsNullOrEmpty(credential.EncryptedPassword))
            {
                credential.EncryptedPassword = _encryptionService.Encrypt(credential.EncryptedPassword);
            }

            return await _credentialRepository.AddAsync(credential);
        }

        public async Task<ServerCredential> UpdateCredentialAsync(ServerCredential credential)
        {
            var existingCredential = await _credentialRepository.GetByIdAsync(credential.Id);

            // Only encrypt if password has changed
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

        public async Task<bool> TestConnectionAsync(ServerCredential credential)
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

            var sourceResult = await TestConnectionAsync(sourceCredential);
            var destinationResult = await TestConnectionAsync(destinationCredential);

            return sourceResult && destinationResult;
        }

        #endregion

        #region Private Helper Methods

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

            // Add files in current directory
            foreach (var item in listing.Where(i => i.Type == FluentFTP.FtpObjectType.File))
            {
                if (string.IsNullOrEmpty(filePattern) || Regex.IsMatch(item.Name, filePattern))
                {
                    files.Add(Path.Combine(folder, item.Name).Replace('\\', '/'));
                }
            }

            // Process subdirectories if requested
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

            // Add files in current directory
            foreach (var item in listing.Where(i => i.IsRegularFile))
            {
                if (string.IsNullOrEmpty(filePattern) || Regex.IsMatch(item.Name, filePattern))
                {
                    files.Add(Path.Combine(folder, item.Name).Replace('\\', '/'));
                }
            }

            // Process subdirectories if requested
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

            // Calculate destination path
            if (createSubfolders && sourceFilePath.StartsWith(sourceBaseFolder))
            {
                // Preserve folder structure
                string relativePath = sourceFilePath.Substring(sourceBaseFolder.Length).TrimStart('/');
                destinationFilePath = Path.Combine(destinationBaseFolder, relativePath).Replace('\\', '/');
            }
            else
            {
                destinationFilePath = Path.Combine(destinationBaseFolder, fileName).Replace('\\', '/');
            }

            // Create record for transferred file
            var transferredFile = new TransferredFile
            {
                TransferExecutionId = executionId,
                FileName = fileName,
                SourcePath = sourceFilePath,
                DestinationPath = destinationFilePath,
                FileSize = 0, // Will be updated during transfer
                TransferSuccessful = false
            };

            try
            {
                // Create temp file to store content during transfer
                string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

                try
                {
                    // Download from source
                    await DownloadFileAsync(source, sourceFilePath, tempFile);

                    // Get file size
                    var fileInfo = new FileInfo(tempFile);
                    transferredFile.FileSize = fileInfo.Length;

                    // Create destination directory if needed
                    string destinationDir = Path.GetDirectoryName(destinationFilePath);
                    await EnsureDirectoryExistsAsync(destination, destinationDir);

                    // Upload to destination
                    await UploadFileAsync(destination, tempFile, destinationFilePath);

                    // Delete source if requested
                    if (deleteSource)
                    {
                        await DeleteFileAsync(source, sourceFilePath);
                    }

                    transferredFile.TransferSuccessful = true;
                }
                finally
                {
                    // Clean up temp file
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

            // Save and return transfer record
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

                default:
                    throw new NotSupportedException($"Server type {credential.ServerType} is not supported");
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

                default:
                    throw new NotSupportedException($"Server type {credential.ServerType} is not supported");
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

                default:
                    throw new NotSupportedException($"Server type {credential.ServerType} is not supported");
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

                        // Split path into segments and create each directory level
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
                                // Continue anyway - might be created by another process
                            }
                        }
                    }
                    break;

                default:
                    throw new NotSupportedException($"Server type {credential.ServerType} is not supported");
            }
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
                // Use private key authentication
                var keyFile = new PrivateKeyFile(credential.PrivateKeyPath);
                return new SftpClient(
                    credential.Host,
                    credential.Port,
                    credential.Username,
                    keyFile);
            }
            else
            {
                // Use password authentication
                string password = _encryptionService.Decrypt(credential.EncryptedPassword);
                return new SftpClient(
                    credential.Host,
                    credential.Port,
                    credential.Username,
                    password);
            }
        }

        #endregion
    }
}
