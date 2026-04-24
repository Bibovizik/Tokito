using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Tokito.DTOs.Common;
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
        public async Task<IActionResult> GetLibrary([FromQuery] string? genre = null, [FromQuery] PaginationQueryDto? pagination = null)
        {
            var userId = TryGetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized("Invalid user token.");
            }

            if (pagination?.IsSpecified == true)
            {
                var pagedLibraryGames = await _gameService.GetLibraryPagedAsync(
                    userId.Value,
                    pagination.ResolvedPage,
                    pagination.ResolvedPageSize,
                    genre,
                    User.FindFirst("countryCode")?.Value);

                return Ok(pagedLibraryGames);
            }

            var libraryGames = await _gameService.GetLibraryAsync(
                userId.Value,
                genre,
                User.FindFirst("countryCode")?.Value);

            return Ok(libraryGames);
        }

        [Authorize(Roles = "Publisher")]
        [HttpPost]
        [Consumes("multipart/form-data")]
        [EndpointDescription("Create a game, optionally upload an image, and seed prices for all supported pricing markets")]
        public async Task<IActionResult> CreateGame([FromForm] GameUpsertFormDto request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!TryMapCreateGameDto(request, out var dto, out var parseError))
            {
                return BadRequest(new { message = parseError });
            }

            var publisherId = GetPublisherId();
            if (!publisherId.HasValue)
            {
                return Forbid();
            }

            try
            {
                var createdGame = await _gameService.CreateGameAsync(
                    publisherId.Value,
                    dto,
                    request.Image,
                    cancellationToken);
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
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateGame([FromRoute] int id, [FromForm] GameUpsertFormDto request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (!TryMapUpdateGameDto(request, out var dto, out var parseError))
            {
                return BadRequest(new { message = parseError });
            }

            var publisherId = GetPublisherId();
            if (!publisherId.HasValue)
            {
                return Forbid();
            }

            try
            {
                var updatedGame = await _gameService.UpdateGameAsync(
                    id,
                    publisherId.Value,
                    dto,
                    request.Image,
                    request.PreserveExistingImage,
                    cancellationToken);
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
        public async Task<IActionResult> GetGamesByGenres(
            [FromQuery] string? genre,
            [FromQuery] string? countryCode = null,
            [FromQuery] PaginationQueryDto? pagination = null)
        {
            if (pagination?.IsSpecified == true)
            {
                var pagedGames = await _gameService.GetGamesByGenresPagedAsync(
                    genre,
                    pagination.ResolvedPage,
                    pagination.ResolvedPageSize,
                    TryGetCurrentUserId(),
                    User.FindFirst("countryCode")?.Value ?? countryCode);

                return Ok(pagedGames);
            }

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

        private static bool TryMapCreateGameDto(GameUpsertFormDto request, out CreateGameDto dto, out string? error)
        {
            dto = new CreateGameDto();

            if (!TryParseJsonElement(request.SystemRequirementsJson, nameof(request.SystemRequirementsJson), out var systemRequirements, out error) ||
                !TryParseGenreIds(request.GenreIdsJson, out var genreIds, out error) ||
                !TryParseCreateMarketPriceOverrides(request.MarketPriceOverridesJson, out var marketPriceOverrides, out error))
            {
                return false;
            }

            dto = new CreateGameDto
            {
                Name = request.Name,
                ReleaseDate = request.ReleaseDate,
                SystemRequirements = systemRequirements,
                MostOneTimePlayers = request.MostOneTimePlayers,
                Description = request.Description,
                BasePriceUah = request.BasePriceUah,
                ImageUrl = request.ImageUrl,
                GenreIds = genreIds,
                MarketPriceOverrides = marketPriceOverrides
            };

            error = null;
            return true;
        }

        private static bool TryMapUpdateGameDto(GameUpsertFormDto request, out UpdateGameDto dto, out string? error)
        {
            dto = new UpdateGameDto();

            if (!TryParseJsonElement(request.SystemRequirementsJson, nameof(request.SystemRequirementsJson), out var systemRequirements, out error) ||
                !TryParseGenreIds(request.GenreIdsJson, out var genreIds, out error) ||
                !TryParseUpdateMarketPriceOverrides(request.MarketPriceOverridesJson, out var marketPriceOverrides, out error))
            {
                return false;
            }

            dto = new UpdateGameDto
            {
                Name = request.Name,
                ReleaseDate = request.ReleaseDate,
                SystemRequirements = systemRequirements,
                MostOneTimePlayers = request.MostOneTimePlayers,
                Description = request.Description,
                BasePriceUah = request.BasePriceUah,
                ImageUrl = request.ImageUrl,
                GenreIds = genreIds,
                MarketPriceOverrides = marketPriceOverrides
            };

            error = null;
            return true;
        }

        private static bool TryParseJsonElement(string? json, string fieldName, out JsonElement? value, out string? error)
        {
            value = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = null;
                return true;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                value = document.RootElement.Clone();
                error = null;
                return true;
            }
            catch (JsonException exception)
            {
                error = $"{fieldName} must contain valid JSON. {exception.Message}";
                return false;
            }
        }

        private static bool TryParseGenreIds(string? json, out ICollection<int> genreIds, out string? error)
        {
            genreIds = [];

            if (string.IsNullOrWhiteSpace(json))
            {
                error = null;
                return true;
            }

            try
            {
                var parsedGenreIds = JsonSerializer.Deserialize<List<int>>(json);
                if (parsedGenreIds == null)
                {
                    error = "GenreIdsJson must contain a JSON array of integers.";
                    return false;
                }

                genreIds = parsedGenreIds;
                error = null;
                return true;
            }
            catch (JsonException exception)
            {
                error = $"GenreIdsJson must contain a JSON array of integers. {exception.Message}";
                return false;
            }
        }

        private static bool TryParseCreateMarketPriceOverrides(
            string? json,
            out ICollection<CreateGameMarketPriceOverrideDto> marketPriceOverrides,
            out string? error)
        {
            marketPriceOverrides = [];

            if (string.IsNullOrWhiteSpace(json))
            {
                error = null;
                return true;
            }

            try
            {
                var parsedOverrides = JsonSerializer.Deserialize<List<CreateGameMarketPriceOverrideDto>>(json, CreateJsonSerializerOptions());
                if (parsedOverrides == null)
                {
                    error = "MarketPriceOverridesJson must contain a JSON array.";
                    return false;
                }

                marketPriceOverrides = parsedOverrides;
                error = null;
                return true;
            }
            catch (JsonException exception)
            {
                error = $"MarketPriceOverridesJson must contain a JSON array. {exception.Message}";
                return false;
            }
        }

        private static bool TryParseUpdateMarketPriceOverrides(
            string? json,
            out ICollection<CreateGameMarketPriceOverrideDto>? marketPriceOverrides,
            out string? error)
        {
            marketPriceOverrides = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = null;
                return true;
            }

            try
            {
                marketPriceOverrides = JsonSerializer.Deserialize<List<CreateGameMarketPriceOverrideDto>>(json, CreateJsonSerializerOptions());
                if (marketPriceOverrides == null)
                {
                    error = "MarketPriceOverridesJson must contain a JSON array.";
                    return false;
                }

                error = null;
                return true;
            }
            catch (JsonException exception)
            {
                error = $"MarketPriceOverridesJson must contain a JSON array. {exception.Message}";
                return false;
            }
        }

        private static JsonSerializerOptions CreateJsonSerializerOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }
    }
}
