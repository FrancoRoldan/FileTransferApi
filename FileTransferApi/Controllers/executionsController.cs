using Core.Services.Transfer;
using Data.Dtos.FileTransfer;
using Data.Models;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FileTransferApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class executionsController : ControllerBase
    {
        private readonly IFileTransferService _fileTransferService;
        private readonly ILogger<executionsController> _logger;

        public executionsController(
            IFileTransferService fileTransferService,
            ILogger<executionsController> logger)
        {
            _fileTransferService = fileTransferService;
            _logger = logger;
        }

        [HttpGet("tasks/{taskId}")]
        public async Task<ActionResult<IEnumerable<TransferExecutionResponse>>> GetTaskExecutions(int taskId)
        {
            try
            {
                var task = await _fileTransferService.GetTaskByIdAsync(taskId);
                if (task == null)
                    return NotFound($"Task with ID {taskId} not found");

                var executions = await _fileTransferService.GetTaskExecutionsAsync(taskId);
                return Ok(executions.Adapt<List<TransferExecutionResponse>>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving executions for task {taskId}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving executions for task {taskId}");
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TransferExecutionResponse>> GetExecutionById(int id)
        {
            try
            {
                var execution = await _fileTransferService.GetExecutionByIdAsync(id);
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

        [HttpGet("{id}/files")]
        public async Task<ActionResult<IEnumerable<TransferredFileResponse>>> GetExecutionFiles(int id)
        {
            try
            {
                var execution = await _fileTransferService.GetExecutionByIdAsync(id);
                if (execution == null)
                    return NotFound($"Execution with ID {id} not found");

                var files = await _fileTransferService.GetExecutionFilesAsync(id);
                return Ok(files.Adapt<List<TransferredFileResponse>>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving files for execution {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving files for execution {id}");
            }
        }

        [HttpPost("{id}/cancel")]
        public async Task<ActionResult> CancelExecution(int id)
        {
            try
            {
                var result = await _fileTransferService.CancelExecutionAsync(id);
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
