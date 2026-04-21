using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tokito.DTOs.WalletDTOs;
using Tokito.Services.Wallets;

namespace Tokito.Controllers
{
    [ApiController]
    [Route("api/wallet")]
    [Authorize]
    public class WalletController : ControllerBase
    {
        private readonly IWalletService _walletService;

        public WalletController(IWalletService walletService)
        {
            _walletService = walletService;
        }

        [HttpGet]
        public async Task<IActionResult> GetWallet()
        {
            var userId = TryGetCurrentUserId();

            if (!userId.HasValue)
            {
                return Unauthorized("Invalid user token.");
            }

            var wallet = await _walletService.GetWalletAsync(userId.Value);
            return Ok(wallet);
        }

        [HttpPost("top-up")]
        public async Task<IActionResult> TopUp([FromBody] WalletTopUpDto dto)
        {
            var userId = TryGetCurrentUserId();

            if (!userId.HasValue)
            {
                return Unauthorized("Invalid user token.");
            }

            try
            {
                var result = await _walletService.TopUpAsync(userId.Value, dto);
                return Ok(result);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        private int? TryGetCurrentUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(userIdString, out int userId)
                ? userId
                : null;
        }
    }
}
