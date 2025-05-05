using Core.Services.Transfer;
using Cronos;
using Data.Interfaces;
using Data.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Core.Schedulers
{
    public class FileTransferScheduler : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<FileTransferScheduler> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

        public FileTransferScheduler(
            IServiceProvider serviceProvider,
            ILogger<FileTransferScheduler> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("File Transfer Scheduler started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessScheduledTasks(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing scheduled tasks");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }

            _logger.LogInformation("File Transfer Scheduler stopped");
        }

        private async Task ProcessScheduledTasks(CancellationToken stoppingToken)
        {
            DateTime now = DateTime.Now;

            using (var scope = _serviceProvider.CreateScope())
            {
                var transferService = scope.ServiceProvider.GetRequiredService<IFileTransferService>();
                var taskRepository = scope.ServiceProvider.GetRequiredService<IFileTransferTaskRepository>();
                var _transferTimeSlotRepository = scope.ServiceProvider.GetRequiredService<ITransferTimeSlotRepository>();

                // Get all active tasks
                var activeTasks = await taskRepository.GetAllAsync();
                activeTasks = activeTasks.Where(t => t.IsActive);

                foreach (FileTransferTask task in activeTasks)
                {
                    var timeSlots = await _transferTimeSlotRepository.GetAllByTaskId(task.Id);
                    task.ExecutionTimes = timeSlots.ToList();
                }

                foreach (var task in activeTasks)
                {
                    if (ShouldExecuteTask(task, now))
                    {
                        _logger.LogInformation($"Executing scheduled task: {task.Name} (ID: {task.Id})");

                        // Execute in the background to not block the scheduler
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using (var executionScope = _serviceProvider.CreateScope())
                                {
                                    var executionService = executionScope.ServiceProvider.GetRequiredService<IFileTransferService>();
                                    await executionService.ExecuteTaskAsync(task);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, $"Error executing scheduled task {task.Id}: {ex.Message}");
                            }
                        }, stoppingToken);
                    }
                }
            }
        }

        private bool ShouldExecuteTask(FileTransferTask task, DateTime currentTimeUtc)
        {
            if (task.ScheduleType == TransferScheduleType.OneTime)
            {
                if (!task.OneTimeExecutionDate.HasValue)
                    return false;

                TimeSpan diff = currentTimeUtc - task.OneTimeExecutionDate.Value;
                return diff >= TimeSpan.Zero && diff <= _checkInterval;
            }

            if (task.ScheduleType == TransferScheduleType.Custom && !string.IsNullOrEmpty(task.CronExpression))
            {
                try
                {
                    var cronExpression = CronExpression.Parse(task.CronExpression);
                    var fromUtc = DateTime.SpecifyKind(currentTimeUtc.AddMinutes(-1), DateTimeKind.Utc);
                    var nextOccurrence = cronExpression.GetNextOccurrence(fromUtc, TimeZoneInfo.Utc);

                    return nextOccurrence.HasValue &&
                           nextOccurrence.Value <= currentTimeUtc &&
                           (currentTimeUtc - nextOccurrence.Value) <= _checkInterval;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Invalid cron expression for task {task.Id}: {ex.Message}");
                    return false;
                }
            }

            if (task.ScheduleType == TransferScheduleType.Monthly)
            {
                return task.OneTimeExecutionDate.HasValue &&
                       task.OneTimeExecutionDate.Value.Day == currentTimeUtc.Day;
            }

            List<TimeSpan> executionTimes = task.ExecutionTimes.Select(t => t.ExecutionTime).ToList();
            if (executionTimes.Count == 0)
                return false;

            foreach (var execTime in executionTimes)
            {
                DateTime scheduledTimeToday = new DateTime(
                    currentTimeUtc.Year,
                    currentTimeUtc.Month,
                    currentTimeUtc.Day,
                    execTime.Hours,
                    execTime.Minutes,
                    execTime.Seconds,
                    DateTimeKind.Utc);

                TimeSpan diff = currentTimeUtc - scheduledTimeToday;

                if (diff >= TimeSpan.Zero && diff <= _checkInterval)
                {
                    if (task.ScheduleType == TransferScheduleType.Daily)
                        return true;

                    if (task.ScheduleType == TransferScheduleType.Weekly)
                    {
                        switch (currentTimeUtc.DayOfWeek)
                        {
                            case DayOfWeek.Monday:
                                return task.IsMonday;
                            case DayOfWeek.Tuesday:
                                return task.IsTuesday;
                            case DayOfWeek.Wednesday:
                                return task.IsWednesday;
                            case DayOfWeek.Thursday:
                                return task.IsThursday;
                            case DayOfWeek.Friday:
                                return task.IsFriday;
                            case DayOfWeek.Saturday:
                                return task.IsSaturday;
                            case DayOfWeek.Sunday:
                                return task.IsSunday;
                            default:
                                return false;
                        }
                    }
                }
            }

            return false;
        }
    }
}
