using Core.Services.TasksManagement;
using Data.Dtos.FileTransfer;
using Data.Interfaces;
using Data.Models;
using Mapster;

namespace Core.Services.TasksManagement
{
    public class TasksManagementService : ITasksManagementService
    {
        private readonly IFileTransferTaskRepository _taskRepository;
        private readonly ITransferTimeSlotRepository _transferTimeSlotRepository;

        public TasksManagementService(
            IFileTransferTaskRepository taskRepository, 
            ITransferTimeSlotRepository transferTimeSlotRepository)
        {
            _taskRepository = taskRepository;
            _transferTimeSlotRepository = transferTimeSlotRepository;
        }

        public async Task<FileTransferTask> CreateTaskAsync(FileTransferTask task)
        {
            return await _taskRepository.AddAsync(task);
        }

        public async Task<FileTransferTask> UpdateTaskAsync(FileTransferTask task)
        {
            var existingTask = await _taskRepository.GetByIdAsync(task.Id);
            if (existingTask == null)
                throw new InvalidOperationException("Task not found");

            var timeSlots = await _transferTimeSlotRepository.GetAllByTaskId(task.Id);
            existingTask.ExecutionTimes = timeSlots.ToList();

            foreach (var exec in existingTask.ExecutionTimes.ToList())
            {
                await _transferTimeSlotRepository.DeleteAsync(exec.Id);
            }

            existingTask.ExecutionTimes.Clear();

            if (task.ExecutionTimes != null && task.ExecutionTimes.Any())
            {
                foreach (var slotDto in task.ExecutionTimes)
                {
                    var newTimeSlot = new TransferTimeSlot
                    {
                        FileTransferTaskId = task.Id,
                        ExecutionTime = slotDto.ExecutionTime,
                        CreatedAt = DateTime.Now
                    };

                    existingTask.ExecutionTimes.Add(newTimeSlot);
                }
            }


            task.Adapt(existingTask);

            return await _taskRepository.UpdateAsync(existingTask);
        }
        public async Task<bool> DeleteTaskAsync(int taskId)
        {
            var task = await _taskRepository.GetByIdAsync(taskId);
            if (task == null)
                return false;

            var timeSlots = await _transferTimeSlotRepository.GetAllByTaskId(task.Id);
            task.ExecutionTimes = timeSlots.ToList();

            foreach (var exec in task.ExecutionTimes.ToList())
            {
                await _transferTimeSlotRepository.DeleteAsync(exec.Id);
            }


            await _taskRepository.DeleteAsync(taskId);
            return true;
        }

        public async Task<FileTransferTask?> GetTaskByIdAsync(int taskId)
        {
            return await _taskRepository.GetByIdAsync(taskId);
        }

        public async Task<PaginatedResponseDto<FileTransferTaskResponse>> GetPaginatedTasksAsync(int pageIndex, int pageSize, string searchTerm = "")
        {
            var totalCount = await _taskRepository.CountAsync(searchTerm);

            var tasks = await _taskRepository.GetPaginatedAsync(pageIndex, pageSize, searchTerm);

            foreach (var task in tasks)
            {
                var timeSlots = await _transferTimeSlotRepository.GetAllByTaskId(task.Id);
                task.ExecutionTimes = timeSlots.ToList();
            }

            return new PaginatedResponseDto<FileTransferTaskResponse>
            {
                Items = tasks.OrderBy(f => f.Id).Adapt<List<FileTransferTaskResponse>>(),
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            };
        }

        public async Task<IEnumerable<FileTransferTask>> GetAllTasksAsync()
        {
            IEnumerable<FileTransferTask> tasks = await _taskRepository.GetAllAsync();

            foreach (FileTransferTask task in tasks)
            {
                var timeSlots = await _transferTimeSlotRepository.GetAllByTaskId(task.Id);
                task.ExecutionTimes = timeSlots.ToList();
            }

            return tasks.OrderBy(x => x.Id);
        }

        public async Task<IEnumerable<FileTransferTask>> GetActiveTasksAsync()
        {
            var allTasks = await _taskRepository.GetAllAsync();
            return allTasks.Where(t => t.IsActive);
        }
    }
}