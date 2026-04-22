using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.Security.Claims;
using Tokito.DTOs.GameDTOs;
using Tokito.DTOs.GameReviewDTOs;
using Tokito.Services.Auth;
using Tokito.Services.Games;
using Tokito.Services.Statuses.GameStatuses;

namespace Tokito.Controllers
{
    [ApiController]
    [Route("api/games")]
    public class GameController : ControllerBase
    {
        public IGameService _gameService;
        public IMapper _mapper;
        public GameController(IGameService gameService, IMapper mapper)
        {
            _gameService = gameService;
            _mapper = mapper;
        }
        [EndpointDescription("Search game by id in route")]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetGameById([FromRoute] int id, [FromQuery] string? countryCode = null)
        {
            var userId = TryGetCurrentUserId();
            var effectiveCountryCode = User.FindFirst("countryCode")?.Value ?? countryCode;
            var game = await _gameService.GetGameByIdAsync(id, userId, effectiveCountryCode);
            if (game == null)
            {
                return NotFound();
            }
            return Ok(game);
        }
        [Authorize(Roles = "User")]
        [HttpPost("createReview/{gameId}")]
        [EndpointDescription("Post a review for a game")]
        public async Task<IActionResult> PostGameReview([FromRoute] int gameId, [FromBody] CreateReviewDto reviewDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized("Invalid user token.");
            }
            var result = await _gameService.AddReviewAsync(userId, gameId, reviewDto);

            if (result.Status == ReviewStatus.AlreadyReviewed)
                return BadRequest(result);

            return Ok("Success");
        }

        [Authorize]
        [HttpPost("{gameId}/purchase")]
        [EndpointDescription("Buy game by id")]

        public async Task<IActionResult> PurchaseGame([FromRoute] int gameId)
        {
            var userId = TryGetCurrentUserId();

            if (!userId.HasValue)
            {
                return Unauthorized("Invalid user token.");
            }

            var result = await _gameService.PurchaseGameAsync(userId.Value, gameId);

            return result.Status switch
            {
                PurchaseGameStatus.Success => Ok(result.Receipt),
                PurchaseGameStatus.GameNotFound => NotFound(new { message = result.Message }),
                PurchaseGameStatus.UserNotFound => Unauthorized(new { message = result.Message }),
                PurchaseGameStatus.AlreadyOwned => Conflict(new { message = result.Message }),
                PurchaseGameStatus.InsufficientFunds => BadRequest(new { message = result.Message }),
                PurchaseGameStatus.ConcurrencyConflict => Conflict(new { message = result.Message }),
                PurchaseGameStatus.PurchaseConflict => Conflict(new { message = result.Message }),
                _ => BadRequest(new { message = result.Message })
            };
        }

        [HttpGet]
        [Description("Get all games, can pass genre as a query")]
        public async Task<IActionResult> GetGamesByGenres([FromQuery] string? genre)
        {
            var filteredGames = await _gameService.GetGamesByGenresAsync(genre);
            if (filteredGames is null) return NotFound();
            return Ok(filteredGames);
        }
        [Authorize(Roles = "Publisher, Admin")]
        [HttpDelete]
        public async Task<IActionResult> DeleteGameByIdAsync([FromBody] int id)
        {
            var isUserAdmin = User.IsInRole("Admin");

            var publisherIdClaim = User.FindFirst("PublisherId")?.Value;
            int.TryParse(publisherIdClaim, out var publisherId);

            var result = await _gameService.DeleteGameByIdAsync(id, publisherId, isUserAdmin);

            if (!result.IsSuccess)
            {
                return result.Status switch
                {
                    DeleteGameStatus.GameNotFound => NotFound(result.Message),
                    DeleteGameStatus.InsufficientRights => Forbid(),
                    _ => BadRequest(result.Message)
                };
            }

            return Ok(new { Message = $"Deleted game with id: {id}", });
        }
        private int? TryGetCurrentUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(userIdString, out int userId)
                ? userId
                : null;
        }

    };
}
