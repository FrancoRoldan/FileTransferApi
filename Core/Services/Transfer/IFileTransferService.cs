using Data.Dtos.FileTransfer;
using Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Services.Transfer
{
    public interface IFileTransferService
    {
        // Task management
        Task<FileTransferTask> CreateTaskAsync(FileTransferTask task);
        Task<FileTransferTask> UpdateTaskAsync(FileTransferTask task);
        Task<bool> DeleteTaskAsync(int taskId);
        Task<FileTransferTask?> GetTaskByIdAsync(int taskId);
        Task<PaginatedResponseDto<FileTransferTaskResponse>> GetPaginatedTasksAsync(int pageIndex, int pageSize);
        Task<IEnumerable<FileTransferTask>> GetAllTasksAsync();
        Task<IEnumerable<FileTransferTask>> GetActiveTasksAsync();

        // Execution
        Task<TransferExecution> ExecuteTaskAsync(int taskId);
        Task<TransferExecution> ExecuteTaskAsync(FileTransferTask task);
        Task<bool> CancelExecutionAsync(int executionId);

        // Execution history
        Task<IEnumerable<TransferExecution>> GetTaskExecutionsAsync(int taskId);
        Task<TransferExecution?> GetExecutionByIdAsync(int executionId);
        Task<IEnumerable<TransferredFile>> GetExecutionFilesAsync(int executionId);

        // Server credential management
        Task<ServerCredential> CreateCredentialAsync(ServerCredential credential);
        Task<ServerCredential> UpdateCredentialAsync(ServerCredential credential);
        Task<bool> DeleteCredentialAsync(int credentialId);
        Task<ServerCredential?> GetCredentialByIdAsync(int credentialId);
        Task<PaginatedResponseDto<ServerCredential>> GetPaginatedCredentialsAsync(int pageIndex, int pageSize,string searchTerm);
        Task<IEnumerable<ServerCredential>> GetAllCredentialsAsync();

        // Testing connections
        Task<bool> TestConnectionAsync(ServerCredential credential, string? folder = "");
        Task<bool> TestTaskConnectionsAsync(FileTransferTask task);
    }
}
