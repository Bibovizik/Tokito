using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tokito.DTOs.UserDTOs;
using Tokito.Services.Auth;

namespace Tokito.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        public IAuthService _authService;
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }
        [HttpGet("status")]
        public IActionResult UserStatus()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated) 
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var email = User.FindFirstValue(ClaimTypes.Email);

                var username = User.Identity.Name;
                var countryCode = User.FindFirst("countryCode")?.Value;
                var publisherId = User.FindFirst("PublisherId")?.Value;
                var roles = User.FindAll(ClaimTypes.Role)
                    .Select(claim => claim.Value)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return Ok(new
                {
                    id = userId,
                    email = email,
                    username = username,
                    countryCode = countryCode,
                    publisherId = publisherId,
                    roles = roles
                });
            }
            return Unauthorized();
        }
        [HttpPost("logout")]
        public async Task<IActionResult> LogoutUser()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated) return Unauthorized();

            await _authService.LogoutAsync();

            return Ok("User.Identity");
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
    }
}
