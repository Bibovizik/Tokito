using Tokito.DTOs.Genres;

namespace Tokito.Services.Genres
{
    public interface IGenreService
    {
        Task<IReadOnlyCollection<GenreViewDto>> GetGenresAsync(CancellationToken cancellationToken = default);

        Task<GenreViewDto?> GetGenreByIdAsync(int genreId, CancellationToken cancellationToken = default);

        Task<GenreViewDto> CreateGenreAsync(GenreUpsertDto dto, CancellationToken cancellationToken = default);

        Task<GenreViewDto> UpdateGenreAsync(int genreId, GenreUpsertDto dto, CancellationToken cancellationToken = default);

        Task DeleteGenreAsync(int genreId, CancellationToken cancellationToken = default);
    }
}
