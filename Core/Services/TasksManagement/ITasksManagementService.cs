using Data.Dtos.FileTransfer;
using Data.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Services.TasksManagement
{
    public interface ITasksManagementService
    {
        Task<FileTransferTask> CreateTaskAsync(FileTransferTask task);
        Task<FileTransferTask> UpdateTaskAsync(FileTransferTask task);
        Task<bool> DeleteTaskAsync(int taskId);
        Task<FileTransferTask?> GetTaskByIdAsync(int taskId);
        Task<PaginatedResponseDto<FileTransferTaskResponse>> GetPaginatedTasksAsync(int pageIndex, int pageSize, string searchTerm = "");
        Task<IEnumerable<FileTransferTask>> GetAllTasksAsync();
        Task<IEnumerable<FileTransferTask>> GetActiveTasksAsync();
    }
}