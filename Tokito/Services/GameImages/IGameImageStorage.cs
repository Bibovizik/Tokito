using Microsoft.AspNetCore.Http;

namespace Tokito.Services.GameImages
{
    public interface IGameImageStorage
    {
        Task<string> SaveGameImageAsync(int gameId, IFormFile image, CancellationToken cancellationToken = default);

        Task DeleteImageAsync(string? imageUrl, CancellationToken cancellationToken = default);
    }
}
