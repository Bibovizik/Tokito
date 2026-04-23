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
