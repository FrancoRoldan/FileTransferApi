using Core.Security;
using Core.Services.FileOperations;
using Core.Services.PatternProcessor;
using Core.Utils;
using Data.Interfaces;
using Data.Models;
using FluentFTP;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Core.Services.FileOperations
{
    public class FileOperationsService : IFileOperationsService
    {
        private readonly IRepository<TransferredFile> _transferredFileRepository;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<FileOperationsService> _logger;
        private readonly IPatternProcessorService _patternProcessor;

        public FileOperationsService(
            IRepository<TransferredFile> transferredFileRepository,
            IEncryptionService encryptionService,
            ILogger<FileOperationsService> logger,
            IPatternProcessorService patternProcessor)
        {
            _transferredFileRepository = transferredFileRepository;
            _encryptionService = encryptionService;
            _logger = logger;
            _patternProcessor = patternProcessor;
        }

        public async Task<IEnumerable<string>> GetFilesFromServerAsync(
            ServerCredential credential,
            string folder,
            ProcessedPattern processedPattern,
            bool includeSubfolders = false)
        {
            List<string> files = new List<string>();

            switch (credential.ServerType.ToUpper())
            {
                case "FTP":
                    using (var client = CreateFtpClient(credential))
                    {
                        await Task.Run(() => client.Connect());
                        await GetFtpFilesRecursiveAsync(client, folder, processedPattern, includeSubfolders, files);
                    }
                    break;

                case "SFTP":
                    using (var client = CreateSftpClient(credential))
                    {
                        await Task.Run(() => client.Connect());
                        await GetSftpFilesRecursiveAsync(client, folder, processedPattern, includeSubfolders, files);
                    }
                    break;

                case "NETWORK":
                    var networkFiles = await GetNetworkFilesAsync(credential, folder, processedPattern, includeSubfolders);
                    files.AddRange(networkFiles);
                    break;

                default:
                    throw new NotSupportedException($"Server type {credential.ServerType} is not supported");
            }

            return files;
        }

        public async Task<TransferredFile> TransferFileAsync(
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
                string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

                try
                {
                    await DownloadFileAsync(source, sourceFilePath, tempFile);

                    var fileInfo = new FileInfo(tempFile);
                    transferredFile.FileSize = fileInfo.Length;

                    string destinationDir = Path.GetDirectoryName(destinationFilePath)!;
                    await EnsureDirectoryExistsAsync(destination, destinationDir);

                    await UploadFileAsync(destination, tempFile, destinationFilePath);

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

        public async Task DeleteFoldersRecursivelyAsync(
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

        public async Task DeleteNetworkFoldersRecursivelyAsync(string baseFolder, HashSet<string> processedFolders)
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

        private async Task GetFtpFilesRecursiveAsync(
            FtpClient client,
            string folder,
            ProcessedPattern processedPattern,
            bool includeSubfolders,
            List<string> files)
        {
            var listing = client.GetListing(folder);
            var regex = new Regex(processedPattern.RegexPattern, RegexOptions.IgnoreCase);

            foreach (var item in listing.Where(i => i.Type == FluentFTP.FtpObjectType.File))
            {
                // Filtro 1: Por nombre usando regex
                if (!regex.IsMatch(item.Name))
                    continue;

                // Filtro 2: Por fecha de modificación (si aplica)
                if (processedPattern.RequiresDateFilter)
                {
                    DateTime fileModified = item.Modified;
                    if (!_patternProcessor.MatchesDateFilter(fileModified, processedPattern.DaysBack!.Value))
                    {
                        _logger.LogDebug("File '{File}' excluded by date filter (modified: {Date})",
                            item.Name, fileModified);
                        continue;
                    }
                }

                files.Add(Path.Combine(folder, item.Name).Replace('\\', '/'));
            }

            if (includeSubfolders)
            {
                foreach (var item in listing.Where(i => i.Type == FluentFTP.FtpObjectType.Directory))
                {
                    string subFolder = Path.Combine(folder, item.Name).Replace('\\', '/');
                    await GetFtpFilesRecursiveAsync(client, subFolder, processedPattern, true, files);
                }
            }
        }

        private async Task GetSftpFilesRecursiveAsync(
            SftpClient client,
            string folder,
            ProcessedPattern processedPattern,
            bool includeSubfolders,
            List<string> files)
        {
            var listing = client.ListDirectory(folder);
            var regex = new Regex(processedPattern.RegexPattern, RegexOptions.IgnoreCase);

            foreach (var item in listing.Where(i => i.IsRegularFile))
            {
                // Filtro 1: Por nombre
                if (!regex.IsMatch(item.Name))
                    continue;

                // Filtro 2: Por fecha (si aplica)
                if (processedPattern.RequiresDateFilter)
                {
                    DateTime fileModified = item.LastWriteTime;
                    if (!_patternProcessor.MatchesDateFilter(fileModified, processedPattern.DaysBack!.Value))
                    {
                        _logger.LogDebug("File '{File}' excluded by date filter (modified: {Date})",
                            item.Name, fileModified);
                        continue;
                    }
                }

                files.Add(Path.Combine(folder, item.Name).Replace('\\', '/'));
            }

            if (includeSubfolders)
            {
                foreach (var item in listing.Where(i => i.IsDirectory && !i.Name.Equals(".") && !i.Name.Equals("..")))
                {
                    string subFolder = Path.Combine(folder, item.Name).Replace('\\', '/');
                    await GetSftpFilesRecursiveAsync(client, subFolder, processedPattern, true, files);
                }
            }
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
                    string networkPath;

                    if (remotePath.StartsWith("/"))
                    {
                        remotePath = remotePath.TrimStart('/');
                    }

                    networkPath = BuildNetworkPath(credential, remotePath);

                    string directory = Path.GetDirectoryName(networkPath)!;
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (!string.IsNullOrEmpty(credential.Username))
                    {
                        string basePath = credential.Host.StartsWith("\\\\") ? credential.Host : $"\\\\{credential.Host}";
                        using (var networkConnection = new NetworkConnection(
                            basePath, credential.Username, _encryptionService.Decrypt(credential.EncryptedPassword!)))
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
                            basePath, credential.Username, _encryptionService.Decrypt(credential.EncryptedPassword!)))
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

        private async Task<IEnumerable<string>> GetNetworkFilesAsync(
            ServerCredential credential,
            string folder,
            ProcessedPattern processedPattern,
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
                        files = await ScanNetworkFolderAsync(networkPath, processedPattern, includeSubfolders);
                    }
                }
                else
                {
                    files = await ScanNetworkFolderAsync(networkPath, processedPattern, includeSubfolders);
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
            ProcessedPattern processedPattern,
            bool includeSubfolders)
        {
            List<string> files = new List<string>();
            var regex = new Regex(processedPattern.RegexPattern, RegexOptions.IgnoreCase);

            try
            {
                foreach (var file in Directory.GetFiles(networkPath))
                {
                    string fileName = Path.GetFileName(file);

                    // Filtro 1: Por nombre
                    if (!regex.IsMatch(fileName))
                        continue;

                    // Filtro 2: Por fecha (si aplica)
                    if (processedPattern.RequiresDateFilter)
                    {
                        var fileInfo = new FileInfo(file);
                        DateTime fileModified = fileInfo.LastWriteTime;

                        if (!_patternProcessor.MatchesDateFilter(fileModified, processedPattern.DaysBack!.Value))
                        {
                            _logger.LogDebug("File '{File}' excluded by date filter (modified: {Date})",
                                fileName, fileModified);
                            continue;
                        }
                    }

                    files.Add(file.Replace('\\', '/'));
                }

                if (includeSubfolders)
                {
                    foreach (var subDir in Directory.GetDirectories(networkPath))
                    {
                        var subDirFiles = await ScanNetworkFolderAsync(subDir, processedPattern, true);
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
    }
}
