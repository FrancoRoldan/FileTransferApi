using Core.Security;
using Core.Services;
using Data.Dtos.Users;
using Data.Models;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace FileTransferApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class usersController : ControllerBase
    {
        private readonly ILogger<usersController> _logger;
        private readonly IUserService _userService;
        public usersController(ILogger<usersController> logger, IUserService userService)
        {
            _logger = logger;
            _userService = userService;
        }

        [Authorize]
        [HttpGet("")]
        public async Task<IActionResult> getAllUsers()
        {
            try
            {
                return Ok(await _userService.GetAllAsync());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> createUsuario(AddUserRequest req)
        {
            try
            {
                User model = await _userService.RegisterAsync(req, req.Password);

                var (user, token) = await _userService.AuthenticateAsync(req.Email, req.Password);

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
    }
}
