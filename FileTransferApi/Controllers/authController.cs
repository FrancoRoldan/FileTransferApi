using Core.Security;
using Core.Services;
using Data.Dtos.Login;
using Data.Dtos.Users;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace FileTransferApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class authController : ControllerBase
    {
        private readonly ILogger<authController> _logger;
        private readonly IUserService _userService;
        private readonly IJwtService _jwtService;
        public authController(ILogger<authController> logger, IUserService userService, IJwtService jwtService)
        {
            _logger = logger;
            _userService = userService;
            _jwtService = jwtService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest model)
        {
            try
            {
                var (user, token) = await _userService.AuthenticateAsync(model.email, model.password);
                
                if (user == null)
                    return Unauthorized("Usuario o contraseña no válidos.");

                GetUserResponse userResponse = user.Adapt<GetUserResponse>();

                return Ok(new { token, user = userResponse });
            }
            catch (ValidationException ex)
            {
                _logger.LogError(ex.Message);
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("refresh")]
        public IActionResult RefreshToken()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                string? token = _jwtService.ExtractTokenFromHeader(authHeader ?? "");

                if (string.IsNullOrEmpty(token))
                    return Unauthorized();

                string newToken = _jwtService.RefreshToken(token);

                if (string.IsNullOrEmpty(newToken))
                    return Unauthorized();

                return Ok(new { token = newToken });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
            
        }
    }
}
