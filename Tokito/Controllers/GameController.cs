using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Tokito.DTOs.GameDTOs;
using Tokito.DTOs.GameReviewDTOs;
using Tokito.Services.Games;
using Tokito.Services.Statuses.GameStatuses;

namespace Tokito.Controllers
{
    [ApiController]
    [Route("api/games")]
    public class GameController : ControllerBase
    {
        private readonly IGameService _gameService;

        public GameController(IGameService gameService)
        {
            _gameService = gameService;
        }

        [Authorize(Roles = "Publisher,Admin")]
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard(
            [FromQuery] DateOnly? dateFrom,
            [FromQuery] DateOnly? dateTo,
            [FromQuery] int? gameId,
            [FromQuery] int? publisherId,
            CancellationToken cancellationToken)
        {
            try
            {
                var dashboard = await _gameService.GetDashboardAsync(
                    GetPublisherId(),
                    User.IsInRole("Admin"),
                    dateFrom,
                    dateTo,
                    gameId,
                    publisherId,
                    cancellationToken);

                return Ok(dashboard);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
        }

        [EndpointDescription("Search game by id in route")]
        [HttpGet("{id:int}")]
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

        [Authorize]
        [HttpGet("library")]
        [EndpointDescription("Get purchased games for the current user")]
        public async Task<IActionResult> GetLibrary([FromQuery] string? genre = null)
        {
            var userId = TryGetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("Invalid user token.");
            }

            var libraryGames = await _gameService.GetLibraryAsync(
                userId.Value,
                genre,
                User.FindFirst("countryCode")?.Value);

            return Ok(libraryGames);
        }

        [Authorize(Roles = "Publisher")]
        [HttpPost]
        [EndpointDescription("Create a game and seed prices for all supported pricing markets")]
        public async Task<IActionResult> CreateGame([FromBody] CreateGameDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var publisherId = GetPublisherId();
            if (!publisherId.HasValue)
            {
                return Forbid();
            }

            try
            {
                var createdGame = await _gameService.CreateGameAsync(publisherId.Value, dto, cancellationToken);
                return CreatedAtAction(nameof(GetGameById), new { id = createdGame.GameId }, createdGame);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
            catch (HttpRequestException exception)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message });
            }
        }

        [Authorize(Roles = "Publisher")]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateGame([FromRoute] int id, [FromBody] UpdateGameDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var publisherId = GetPublisherId();
            if (!publisherId.HasValue)
            {
                return Forbid();
            }

            try
            {
                var updatedGame = await _gameService.UpdateGameAsync(id, publisherId.Value, dto, cancellationToken);
                return Ok(updatedGame);
            }
            catch (ArgumentException exception)
            {
                return BadRequest(new { message = exception.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (KeyNotFoundException exception)
            {
                return NotFound(new { message = exception.Message });
            }
            catch (HttpRequestException exception)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message });
            }
            catch (InvalidOperationException exception)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = exception.Message });
            }
        }

        [Authorize(Roles = "User")]
        [HttpPost("createReview/{gameId:int}")]
        [EndpointDescription("Post a review for a game")]
        public async Task<IActionResult> PostGameReview([FromRoute] int gameId, [FromBody] CreateReviewDto reviewDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = TryGetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("Invalid user token.");
            }

            var result = await _gameService.AddReviewAsync(userId.Value, gameId, reviewDto);

            if (result.Status == ReviewStatus.AlreadyReviewed)
            {
                return BadRequest(result);
            }

            if (result.Status == ReviewStatus.GameNotFound)
            {
                return NotFound(result);
            }

            if (result.Status == ReviewStatus.UserNotFound)
            {
                return Unauthorized(result);
            }

            if (result.Status == ReviewStatus.GameNotOwned)
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }

            return Ok("Success");
        }

        [Authorize]
        [HttpPost("{gameId:int}/purchase")]
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
        public async Task<IActionResult> GetGamesByGenres([FromQuery] string? genre, [FromQuery] string? countryCode = null)
        {
            var filteredGames = await _gameService.GetGamesByGenresAsync(
                genre,
                TryGetCurrentUserId(),
                User.FindFirst("countryCode")?.Value ?? countryCode);

            return Ok(filteredGames);
        }

        [Authorize(Roles = "Publisher, Admin")]
        [HttpDelete]
        public async Task<IActionResult> DeleteGameByIdAsync([FromBody, Required] int id)
        {
            var isUserAdmin = User.IsInRole("Admin");
            var publisherId = GetPublisherId() ?? 0;

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

            return Ok(new { Message = $"Deleted game with id: {id}" });
        }

        private int? TryGetCurrentUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(userIdString, out var userId)
                ? userId
                : null;
        }

        private int? GetPublisherId()
        {
            var publisherIdClaim = User.FindFirst("PublisherId")?.Value;

            return int.TryParse(publisherIdClaim, out var publisherId)
                ? publisherId
                : null;
        }
    }
}
