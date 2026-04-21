using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Tokito.DTOs.GameDTOs;
using Tokito.DTOs.GameReviewDTOs;
using Tokito.Services.Auth;
using Tokito.Services.Games;

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
        [HttpGet("{id}")]
        public async Task<IActionResult> GetGameById([FromRoute] int id)
        {
            var game = await _gameService.GetGameByIdAsync(id);
            if (game == null)
            {
                return NotFound();
            }
            return Ok(game);
        }
        [Authorize(Roles = "User")]
        [HttpPost("createReview/{gameId}")]
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

            if (result == 0)
                return BadRequest(result);

            return Ok("Success");
        }

        [HttpGet("genres/{genre}")]
        public async Task<IActionResult> GetGamesByGenres([FromRoute] string genre)
        {
            var filteredGames = await _gameService.GetGamesByGenresAsync(genre);
            if (filteredGames is null) return NotFound();
            return Ok(filteredGames);
        } 

    };
}
