using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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
        public async Task<IActionResult> UserStatus()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated) 
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var email = User.FindFirstValue(ClaimTypes.Email);

                var username = User.Identity.Name;
                var countryCode = User.FindFirst("countryCode")?.Value;

                return Ok(new
                {
                    id = userId,
                    email = email,
                    username = username,
                    countryCode = countryCode
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
    };
}
