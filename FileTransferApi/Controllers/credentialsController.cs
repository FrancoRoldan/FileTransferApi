using Core.Services.Transfer;
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
    public class credentialsController : ControllerBase
    {
        private readonly IFileTransferService _fileTransferService;
        private readonly ILogger<credentialsController> _logger;

        public credentialsController(
            IFileTransferService fileTransferService,
            ILogger<credentialsController> logger)
        {
            _fileTransferService = fileTransferService;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("")]
        public async Task<ActionResult<IEnumerable<ServerCredential>>> GetAllCredentials()
        {
            try
            {
                var credentials = await _fileTransferService.GetAllCredentialsAsync();
                return Ok(credentials);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all credentials");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving credentials");
            }
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<ActionResult<ServerCredential>> GetCredentialById(int id)
        {
            try
            {
                var credential = await _fileTransferService.GetCredentialByIdAsync(id);
                if (credential == null)
                    return NotFound($"Credential with ID {id} not found");

                return Ok(credential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving credential {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error retrieving credential {id}");
            }
        }

        [Authorize]
        [HttpPost("")]
        public async Task<ActionResult<ServerCredential>> CreateCredential([FromBody] ServerCredentialRequest credential)
        {
            try
            {
                var createdCredential = await _fileTransferService.CreateCredentialAsync(credential.Adapt<ServerCredential>());
                return CreatedAtAction(nameof(GetCredentialById), new { id = createdCredential.Id }, createdCredential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating credential");
                return StatusCode(StatusCodes.Status500InternalServerError, "Error creating credential");
            }
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult<ServerCredential>> UpdateCredential(int id, [FromBody] ServerCredential credential)
        {
            try
            {
                if (id != credential.Id)
                    return BadRequest("Credential ID mismatch");

                var existingCredential = await _fileTransferService.GetCredentialByIdAsync(id);
                if (existingCredential == null)
                    return NotFound($"Credential with ID {id} not found");

                var updatedCredential = await _fileTransferService.UpdateCredentialAsync(credential);
                return Ok(updatedCredential);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating credential {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error updating credential {id}");
            }
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteCredential(int id)
        {
            try
            {
                var result = await _fileTransferService.DeleteCredentialAsync(id);
                if (!result)
                    return NotFound($"Credential with ID {id} not found");

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting credential {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error deleting credential {id}");
            }
        }

        [Authorize]
        [HttpPost("{id}/test")]
        public async Task<ActionResult<bool>> TestConnection(int id, string? folder)
        {
            try
            {
                var credential = await _fileTransferService.GetCredentialByIdAsync(id);
                if (credential == null)
                    return NotFound($"Credential with ID {id} not found");

                var result = await _fileTransferService.TestConnectionAsync(credential, folder);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error testing connection for credential {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error testing connection for credential {id}");
            }
        }
    }
}
