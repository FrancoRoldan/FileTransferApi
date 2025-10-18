using Core.Services.ExecutionManagement;
using Core.Services.TasksManagement;
using Data.Dtos.FileTransfer;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FileTransferApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class executionsController : ControllerBase
    {
        private readonly IExecutionManagementService _executionManagementService;
        private readonly ITasksManagementService _tasksManagementService;
        private readonly ILogger<executionsController> _logger;

        public executionsController(
            IExecutionManagementService executionManagementService,
            ITasksManagementService tasksManagementService,
            ILogger<executionsController> logger)
        {
            _executionManagementService = executionManagementService;
            _tasksManagementService = tasksManagementService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("tasks/{taskId}")]
        public async Task<ActionResult<IEnumerable<TransferExecutionResponse>>> GetTaskExecutions(int taskId)
        {
            try
            {
                var task = await _tasksManagementService.GetTaskByIdAsync(taskId);
                if (task == null)
                    return NotFound($"Task with ID {taskId} not found");

                var executions = await _executionManagementService.GetTaskExecutionsAsync(taskId);
                return Ok(executions.Adapt<List<TransferExecutionResponse>>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving executions for task {taskId}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving executions for task {taskId}");
            }
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<TransferExecutionResponse>> GetExecutionById(int id)
        {
            try
            {
                var execution = await _executionManagementService.GetExecutionByIdAsync(id);
                if (execution == null)
                    return NotFound($"Execution with ID {id} not found");

                return Ok(execution.Adapt<TransferExecutionResponse>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving execution {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving execution {id}");
            }
        }

        [Authorize]
        [HttpGet("{id}/files")]
        public async Task<ActionResult<IEnumerable<TransferredFileResponse>>> GetExecutionFiles(int id)
        {
            try
            {
                var execution = await _executionManagementService.GetExecutionByIdAsync(id);
                if (execution == null)
                    return NotFound($"Execution with ID {id} not found");

                var files = await _executionManagementService.GetExecutionFilesAsync(id);
                return Ok(files.Adapt<List<TransferredFileResponse>>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving files for execution {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving files for execution {id}");
            }
        }

        [Authorize]
        [HttpPost("{id}/cancel")]
        public async Task<ActionResult> CancelExecution(int id)
        {
            try
            {
                var result = await _executionManagementService.CancelExecutionAsync(id);
                if (!result)
                    return NotFound($"Execution with ID {id} not found or cannot be cancelled");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cancelling execution {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error cancelling execution {id}");
            }
        }
    }
}
