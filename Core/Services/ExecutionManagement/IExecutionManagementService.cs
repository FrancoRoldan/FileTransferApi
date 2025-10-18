using Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Services.ExecutionManagement
{
    public interface IExecutionManagementService
    {
        Task<TransferExecution> ExecuteTaskAsync(int taskId);
        Task<TransferExecution> ExecuteTaskAsync(FileTransferTask task);
        Task<bool> CancelExecutionAsync(int executionId);
        Task<IEnumerable<TransferExecution>> GetTaskExecutionsAsync(int taskId);
        Task<TransferExecution?> GetExecutionByIdAsync(int executionId);
        Task<IEnumerable<TransferredFile>> GetExecutionFilesAsync(int executionId);
    }
}