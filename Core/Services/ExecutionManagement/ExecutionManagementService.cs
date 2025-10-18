using Core.Services.ExecutionManagement;
using Core.Services.FileOperations;
using Data.Interfaces;
using Data.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Core.Services.ExecutionManagement
{
    public class ExecutionManagementService : IExecutionManagementService
    {
        private readonly IFileTransferTaskRepository _taskRepository;
        private readonly IServerCredentialRepository _credentialRepository;
        private readonly IRepository<TransferExecution> _executionRepository;
        private readonly IRepository<TransferredFile> _transferredFileRepository;
        private readonly IFileOperationsService _fileOperationsService;
        private readonly ILogger<ExecutionManagementService> _logger;

        public ExecutionManagementService(
            IFileTransferTaskRepository taskRepository,
            IServerCredentialRepository credentialRepository,
            IRepository<TransferExecution> executionRepository,
            IRepository<TransferredFile> transferredFileRepository,
            IFileOperationsService fileOperationsService,
            ILogger<ExecutionManagementService> logger)
        {
            _taskRepository = taskRepository;
            _credentialRepository = credentialRepository;
            _executionRepository = executionRepository;
            _transferredFileRepository = transferredFileRepository;
            _fileOperationsService = fileOperationsService;
            _logger = logger;
        }

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
                StartTime = DateTime.Now,
                Status = "In Progress"
            };

            execution = await _executionRepository.AddAsync(execution);

            try
            {
                var sourceCredential = await _credentialRepository.GetByIdAsync(task.SourceCredentialId);
                var destinationCredential = await _credentialRepository.GetByIdAsync(task.DestinationCredentialId);

                if (sourceCredential == null || destinationCredential == null)
                    throw new ArgumentException("Source or destination credential not found");

                var sourceFiles = await _fileOperationsService.GetFilesFromServerAsync(
                    sourceCredential,
                    task.SourceFolder,
                    task.FilePattern,
                    task.CopySubfolders);

                HashSet<string> processedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int filesTransferred = 0;
                int errorCount = 0;

                foreach (var sourceFile in sourceFiles)
                {
                    var result = await _fileOperationsService.TransferFileAsync(
                        execution.Id,
                        sourceCredential,
                        destinationCredential,
                        sourceFile,
                        task.SourceFolder,
                        task.DestinationFolder,
                        task.CreateSubfolders,
                        task.DeleteSourceFolderAfterTransfer);

                    if (result.TransferSuccessful)
                    {
                        filesTransferred++;

                        if (task.DeleteSourceFolderAfterTransfer)
                        {
                            string parentFolder = Path.GetDirectoryName(sourceFile)?.Replace('\\', '/') ?? string.Empty;
                            if (!string.IsNullOrEmpty(parentFolder))
                            {
                                processedFolders.Add(parentFolder);
                            }
                        }
                    }
                    else
                    {
                        errorCount++;
                    }
                }

                if (task.DeleteSourceFolderAfterTransfer)
                {
                    if (sourceCredential.ServerType.ToUpper() == "NETWORK")
                    {

                        string networkPath = "\\\\" + sourceCredential.Host + (task.SourceFolder.StartsWith("/") ? task.SourceFolder : "/" + task.SourceFolder);
                        await _fileOperationsService.DeleteNetworkFoldersRecursivelyAsync(networkPath, processedFolders);
                    }
                    else
                    {
                        await _fileOperationsService.DeleteFoldersRecursivelyAsync(sourceCredential, task.SourceFolder, processedFolders);
                    }
                }

                execution.FilesTransferred = filesTransferred;
                execution.ErrorCount = errorCount;
                execution.EndTime = DateTime.Now;
                execution.Status = "Completed";

                await _executionRepository.UpdateAsync(execution);
                return execution;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing task {task.Id}: {ex.Message}");
                execution.EndTime = DateTime.Now;
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
            execution.EndTime = DateTime.Now;
            await _executionRepository.UpdateAsync(execution);

            return true;
        }

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
    }
}
