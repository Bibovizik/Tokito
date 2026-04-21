using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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
        public IActionResult TestingAuthorize()
        {
            return Ok("Hi!");
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("testAdmin")]
        public IActionResult TestingAdmin()
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

        [Authorize]
        [HttpPost("change-country")]
        public async Task<IActionResult> ChangeCountry([FromBody] ChangeCountryDto dto)
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                return Unauthorized("Invalid user token.");
            }

            try
            {
                var result = await _authService.ChangeCountryAsync(userId, dto);
                return Ok(result);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return NotFound(new { message = exception.Message });
            }
        }
    }
}
