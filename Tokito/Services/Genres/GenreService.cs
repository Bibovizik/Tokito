using Microsoft.EntityFrameworkCore;
using Tokito.Data;
using Tokito.DTOs.Genres;
using Tokito.Models;

namespace Tokito.Services.Genres
{
    public class GenreService : IGenreService
    {
        private readonly GameStore _gameStore;

        public GenreService(GameStore gameStore)
        {
            _gameStore = gameStore;
        }

        public async Task<IReadOnlyCollection<GenreViewDto>> GetGenresAsync(CancellationToken cancellationToken = default)
        {
            return await _gameStore.Genres
                .AsNoTracking()
                .OrderBy(genre => genre.Name)
                .Select(MapGenreViewDtoExpression())
                .ToListAsync(cancellationToken);
        }

        public async Task<GenreViewDto?> GetGenreByIdAsync(int genreId, CancellationToken cancellationToken = default)
        {
            return await _gameStore.Genres
                .AsNoTracking()
                .Where(genre => genre.GenreId == genreId)
                .Select(MapGenreViewDtoExpression())
                .SingleOrDefaultAsync(cancellationToken);
        }

        public async Task<GenreViewDto> CreateGenreAsync(GenreUpsertDto dto, CancellationToken cancellationToken = default)
        {
            var normalizedName = NormalizeName(dto.Name);
            var duplicateExists = await GenreNameExistsAsync(normalizedName, cancellationToken: cancellationToken);
            if (duplicateExists)
            {
                throw new InvalidOperationException("Genre with this name already exists.");
            }

            var genre = new Genre
            {
                Name = normalizedName,
                Description = NormalizeOptionalText(dto.Description)
            };

            _gameStore.Genres.Add(genre);
            await _gameStore.SaveChangesAsync(cancellationToken);

            return MapGenreViewDto(genre);
        }

        public async Task<GenreViewDto> UpdateGenreAsync(int genreId, GenreUpsertDto dto, CancellationToken cancellationToken = default)
        {
            var genre = await _gameStore.Genres
                .SingleOrDefaultAsync(currentGenre => currentGenre.GenreId == genreId, cancellationToken);

            if (genre == null)
            {
                throw new KeyNotFoundException("Genre was not found.");
            }

            var normalizedName = NormalizeName(dto.Name);
            var duplicateExists = await GenreNameExistsAsync(normalizedName, genreId, cancellationToken);
            if (duplicateExists)
            {
                throw new InvalidOperationException("Genre with this name already exists.");
            }

            genre.Name = normalizedName;
            genre.Description = NormalizeOptionalText(dto.Description);

            await _gameStore.SaveChangesAsync(cancellationToken);

            return MapGenreViewDto(genre);
        }

        public async Task DeleteGenreAsync(int genreId, CancellationToken cancellationToken = default)
        {
            var genre = await _gameStore.Genres
                .Include(currentGenre => currentGenre.Games)
                .SingleOrDefaultAsync(currentGenre => currentGenre.GenreId == genreId, cancellationToken);

            if (genre == null)
            {
                throw new KeyNotFoundException("Genre was not found.");
            }

            if (genre.Games.Count > 0)
            {
                throw new InvalidOperationException("Cannot delete a genre that is assigned to games.");
            }

            _gameStore.Genres.Remove(genre);
            await _gameStore.SaveChangesAsync(cancellationToken);
        }

        private async Task<bool> GenreNameExistsAsync(
            string normalizedName,
            int? excludedGenreId = null,
            CancellationToken cancellationToken = default)
        {
            var normalizedUpperName = normalizedName.ToUpperInvariant();

            return await _gameStore.Genres
                .AsNoTracking()
                .AnyAsync(
                    genre => (!excludedGenreId.HasValue || genre.GenreId != excludedGenreId.Value) &&
                             genre.Name.ToUpper() == normalizedUpperName,
                    cancellationToken);
        }

        private static GenreViewDto MapGenreViewDto(Genre genre)
        {
            return new GenreViewDto
            {
                GenreId = genre.GenreId,
                Name = genre.Name,
                Description = genre.Description
            };
        }

        private static System.Linq.Expressions.Expression<Func<Genre, GenreViewDto>> MapGenreViewDtoExpression()
        {
            return genre => new GenreViewDto
            {
                GenreId = genre.GenreId,
                Name = genre.Name,
                Description = genre.Description
            };
        }

        private static string NormalizeName(string name)
        {
            return name.Trim();
        }

        private static string? NormalizeOptionalText(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
