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

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userIdString) || !int.TryParse(userIdString, out var userId))
            {
                return Unauthorized("Invalid user token.");
            }

            try
            {
                var profile = await _authService.GetProfileAsync(userId);
                return Ok(profile);
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
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
        [HttpPost("register-publisher")]
        public async Task<IActionResult> RegisterPublisher([FromBody] PublisherRegistrationDTO publisherRegistrationDTO)
        {
            var result = await _authService.RegisterPublisherAsync(publisherRegistrationDTO);
            return result.Succeeded ? Ok() : BadRequest(result.Errors);
        }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDTO userLoginDTO)
        {
            try
            {
                var result = await _authService.LoginAsync(userLoginDTO);
                if (result.Succeeded)
                {
                    return Ok(new { message = "Login successful" });
                }

                if (result.IsLockedOut)
                    return StatusCode(423, "Account locked");
                if (result.IsNotAllowed)
                    return StatusCode(423, new { message = "Account is blocked" });

                return Unauthorized(new { message = "Invalid email or password" });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new { message = exception.Message });
            }
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

        [Authorize(Roles = "Admin")]
        [HttpPut("{userId:int}/account-status")]
        public async Task<IActionResult> UpdateAccountStatus([FromRoute] int userId, [FromBody] UpdateAccountStatusDto dto)
        {
            try
            {
                await _authService.UpdateAccountStatusAsync(userId, dto.AccountStatus);
                return Ok(new { userId, accountStatus = dto.AccountStatus });
            }
            catch (ArgumentOutOfRangeException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new { message = exception.Message });
            }
        }

        [Authorize(Roles = "Admin")]
        [HttpDelete("{userId:int}")]
        public async Task<IActionResult> DeleteUser([FromRoute] int userId)
        {
            try
            {
                var result = await _authService.DeleteUserAsync(userId);
                return Ok(result);
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new { message = exception.Message });
            }
        }
    }
}
