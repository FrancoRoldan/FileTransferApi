﻿using Core.Services.Transfer;
using Data.Dtos.FileTransfer;
using Data.Models;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FileTransferApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class tasksController : ControllerBase
    {
        private readonly IFileTransferService _fileTransferService;
        private readonly ILogger<tasksController> _logger;

        public tasksController(
            IFileTransferService fileTransferService,
            ILogger<tasksController> logger)
        {
            _fileTransferService = fileTransferService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("paginated")]
        public async Task<ActionResult<PaginatedResponseDto<FileTransferTaskResponse>>> GetPaginatedTasks(
            [FromQuery] int pageIndex = 0,
            [FromQuery] int pageSize = 10,
            [FromQuery] string searchTerm = "")
        {
            try
            {
                var result = await _fileTransferService.GetPaginatedTasksAsync(pageIndex, pageSize, searchTerm);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving paginated tasks");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving paginated tasks");
            }
        }

        [Authorize]
        [HttpGet("")]
        public async Task<ActionResult<IEnumerable<FileTransferTaskResponse>>> GetAllTasks()
        {
            try
            {
                var tasks = await _fileTransferService.GetAllTasksAsync();
                return Ok(tasks.Adapt<List<FileTransferTaskResponse>>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all tasks");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving tasks");
            }
        }

        [Authorize]
        [HttpGet("active")]
        public async Task<ActionResult<IEnumerable<FileTransferTaskResponse>>> GetActiveTasks()
        {
            try
            {
                var tasks = await _fileTransferService.GetActiveTasksAsync();
                return Ok(tasks.Adapt<List<FileTransferTaskResponse>>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active tasks");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving active tasks");
            }
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<FileTransferTaskResponse>> GetTaskById(int id)
        {
            try
            {
                var task = await _fileTransferService.GetTaskByIdAsync(id);
                if (task == null)
                    return NotFound($"Task with ID {id} not found");

                return Ok(task.Adapt<FileTransferTaskResponse>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving task {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving task {id}");
            }
        }

        [Authorize]
        [HttpPost("")]
        public async Task<ActionResult<FileTransferTaskResponse>> CreateTask([FromBody] FileTransferTaskRequest task)
        {
            try
            {
                var createdTask = await _fileTransferService.CreateTaskAsync(task.Adapt<FileTransferTask>());
                return CreatedAtAction(nameof(GetTaskById), new { id = createdTask.Id }, createdTask.Adapt<FileTransferTaskResponse>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating task");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error creating task");
            }
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult<FileTransferTask>> UpdateTask(int id, [FromBody] FileTransferTaskRequest task)
        {
            try
            {
                if (id != task.Id)
                    return BadRequest("Task ID mismatch");

                var existingTask = await _fileTransferService.GetTaskByIdAsync(id);
                if (existingTask == null)
                    return NotFound($"Task with ID {id} not found");

                var updatedTask = await _fileTransferService.UpdateTaskAsync(task.Adapt<FileTransferTask>());
                return Ok(updatedTask.Adapt<FileTransferTaskResponse>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating task {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error updating task {id}");
            }
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTask(int id)
        {
            try
            {
                var result = await _fileTransferService.DeleteTaskAsync(id);
                if (!result)
                    return NotFound($"Task with ID {id} not found");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting task {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error deleting task {id}");
            }
        }

        [Authorize]
        [HttpPost("{id}/execute")]
        public async Task<ActionResult<TransferExecutionResponse>> ExecuteTask(int id)
        {
            try
            {
                var task = await _fileTransferService.GetTaskByIdAsync(id);
                if (task == null)
                    return NotFound($"Task with ID {id} not found");

                var execution = await _fileTransferService.ExecuteTaskAsync(id);
                return Ok(execution.Adapt<TransferExecutionResponse>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error executing task {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error executing task {id}");
            }
        }

        [Authorize]
        [HttpPost("{id}/test")]
        public async Task<ActionResult<bool>> TestTaskConnections(int id)
        {
            try
            {
                var task = await _fileTransferService.GetTaskByIdAsync(id);
                if (task == null)
                    return NotFound($"Task with ID {id} not found");

                var result = await _fileTransferService.TestTaskConnectionsAsync(task);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error testing connections for task {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error testing connections for task {id}");
            }
        }
        
    }
}
