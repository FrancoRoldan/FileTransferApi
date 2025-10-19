using Core.Services.ConnectionTesting;
using Core.Services.Credential;
using Data.Dtos.FileTransfer;
using Data.Models;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace FileTransferApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class credentialsController : ControllerBase
    {
        private readonly IServerCredential _serverCredentialService;
        private readonly IConnectionTestingService _connectionService;
        private readonly ILogger<credentialsController> _logger;
        private readonly IWebHostEnvironment _env;

        public credentialsController(
            IServerCredential serverCredentialService,
            IConnectionTestingService connectionService,
            ILogger<credentialsController> logger,
            IWebHostEnvironment env)
        {
            _serverCredentialService = serverCredentialService;
            _connectionService = connectionService;
            _logger = logger;
            _env = env;
        }

        [Authorize]
        [HttpGet("paginated")]
        public async Task<ActionResult<PaginatedResponseDto<ServerCredential>>> GetPaginatedTasks(
        [FromQuery] int pageIndex = 0,
        [FromQuery] int pageSize = 10,
        [FromQuery] string searchTerm = "")
        {
            try
            {
                var result = await _serverCredentialService.GetPaginatedCredentialsAsync(pageIndex, pageSize, searchTerm);
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
        public async Task<ActionResult<IEnumerable<ServerCredential>>> GetAllCredentials()
        {
            try
            {
                var credentials = await _serverCredentialService.GetAllCredentialsAsync();
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
                var credential = await _serverCredentialService.GetCredentialByIdAsync(id);
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
                var createdCredential = await _serverCredentialService.CreateCredentialAsync(credential.Adapt<ServerCredential>());
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

                var existingCredential = await _serverCredentialService.GetCredentialByIdAsync(id);
                if (existingCredential == null)
                    return NotFound($"Credential with ID {id} not found");

                var updatedCredential = await _serverCredentialService.UpdateCredentialAsync(credential);
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
                var result = await _serverCredentialService.DeleteCredentialAsync(id);
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
                var credential = await _serverCredentialService.GetCredentialByIdAsync(id);
                if (credential == null)
                    return NotFound($"Credential with ID {id} not found");

                var result = await _connectionService.TestConnectionAsync(credential, folder);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error testing connection for credential {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error testing connection for credential {id}");
            }
        }

        [Authorize]
        [HttpPost("{id}/uploadKey")]
        public async Task<ActionResult<ServerCredential>> UploadPrivateKey(int id, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                    return BadRequest("No file uploaded");

                var credential = await _serverCredentialService.GetCredentialByIdAsync(id);
                if (credential == null)
                    return NotFound($"Credential with ID {id} not found");

                var keysFolder = Path.Combine(_env.ContentRootPath, "PrivateKeys");
                if (!Directory.Exists(keysFolder))
                    Directory.CreateDirectory(keysFolder);

                var ext = Path.GetExtension(file.FileName);
                var fileName = $"key_{id}_{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(keysFolder, fileName);

                using (var stream = System.IO.File.Create(filePath))
                {
                    await file.CopyToAsync(stream);
                }

                credential.PrivateKeyPath = filePath;
                var updated = await _serverCredentialService.UpdateCredentialAsync(credential);

                return Ok(updated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading private key for credential {id}");
                return StatusCode(StatusCodes.Status500InternalServerError, $"Error uploading private key for credential {id}");
            }
        }
    }
}
