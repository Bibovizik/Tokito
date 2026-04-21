using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tokito.DTOs.UserDTOs;
using Tokito.Services.Auth;

namespace Tokito.Controllers
{
    [ApiController]
    [Route("api/user")]
    public class UserController : ControllerBase
    {
        public IAuthService _authService { get; set; }
        public UserController(IAuthService authService)
        {
            _authService = authService;
        }
        [Authorize]
        [HttpGet("test")]
        public async Task<IActionResult> TestingAuthorize()
        {
            return Ok("Hi!");
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("testAdmin")]
        public async Task<IActionResult> TestingAdmin()
        {
            return Ok("Hello, mr Admin!");
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Registration([FromBody] UserRegistrationDTO userRegistrationDTO)
        {
            var result = await _authService.RegisterUserAsync(userRegistrationDTO);
            return result.Succeeded ? Ok() : BadRequest(result.Errors);
        }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDTO userLoginDTO)
        {
            var result = await _authService.LoginAsync(userLoginDTO);
            if (result.Succeeded)
            {
                return Ok(new { message = "Login successful" });
            }

            if (result.IsLockedOut)
                return StatusCode(423, "Account locked");

            return Unauthorized(new { message = "Invalid email or password" });
        }
    }
}
