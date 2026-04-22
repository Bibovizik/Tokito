using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tokito.DTOs.GameReviewDTOs;
using Tokito.Services.GameReviews;
using Tokito.Services.Statuses.GameStatuses;

namespace Tokito.Controllers
{
    [ApiController]
    [Route("api/gameReviews")]
    public class GameReviewsController : ControllerBase
    {
        IGameReviewService _gameReviewService;
        public GameReviewsController(IGameReviewService gameReviewService) 
        {
            _gameReviewService = gameReviewService;
        }

        [HttpGet("{id}")]
        [EndpointDescription("Get all reviews for a game")]
        public async Task<IActionResult> GetGameReviewsByid([FromRoute] int id)
        {
            var reviews = await _gameReviewService.GetGameReviewsById(id);
            if (reviews.Count == 0)
            {
                return NotFound("No Reviews were found for this game");
            }
            return Ok(reviews);
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

            var result = await _gameReviewService.PostGameReviewById(userId.Value, gameId, reviewDto);

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
        private int? TryGetCurrentUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(userIdString, out var userId)
                ? userId
                : null;
        }
    }
}
