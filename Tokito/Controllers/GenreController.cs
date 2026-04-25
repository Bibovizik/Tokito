using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tokito.DTOs.Genres;
using Tokito.Services.Genres;

namespace Tokito.Controllers
{
    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/genres")]
    public class GenreController : ControllerBase
    {
        private readonly IGenreService _genreService;

        public GenreController(IGenreService genreService)
        {
            _genreService = genreService;
        }

        [HttpGet]
        public async Task<IActionResult> GetGenres(CancellationToken cancellationToken)
        {
            var genres = await _genreService.GetGenresAsync(cancellationToken);
            return Ok(genres);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetGenreById([FromRoute] int id, CancellationToken cancellationToken)
        {
            var genre = await _genreService.GetGenreByIdAsync(id, cancellationToken);
            return genre == null
                ? NotFound(new { message = "Genre was not found." })
                : Ok(genre);
        }

        [HttpPost]
        public async Task<IActionResult> CreateGenre([FromBody] GenreUpsertDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdGenre = await _genreService.CreateGenreAsync(dto, cancellationToken);
                return CreatedAtAction(nameof(GetGenreById), new { id = createdGenre.GenreId }, createdGenre);
            }
            catch (InvalidOperationException exception)
            {
                return Conflict(new { message = exception.Message });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateGenre([FromRoute] int id, [FromBody] GenreUpsertDto dto, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var updatedGenre = await _genreService.UpdateGenreAsync(id, dto, cancellationToken);
                return Ok(updatedGenre);
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

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteGenre([FromRoute] int id, CancellationToken cancellationToken)
        {
            try
            {
                await _genreService.DeleteGenreAsync(id, cancellationToken);
                return NoContent();
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
