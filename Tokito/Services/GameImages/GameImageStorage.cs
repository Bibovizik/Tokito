using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace Tokito.Services.GameImages
{
    public class GameImageStorage : IGameImageStorage
    {
        private const long MaxImageSizeBytes = 5 * 1024 * 1024;

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".gif"
        };

        private readonly IWebHostEnvironment _webHostEnvironment;

        public GameImageStorage(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<string> SaveGameImageAsync(int gameId, IFormFile image, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(image);

            if (image.Length <= 0)
            {
                throw new ArgumentException("Uploaded image is empty.", nameof(image));
            }

            if (image.Length > MaxImageSizeBytes)
            {
                throw new ArgumentException("Uploaded image exceeds the 5 MB limit.", nameof(image));
            }

            var extension = Path.GetExtension(image.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                throw new ArgumentException("Only .jpg, .jpeg, .png, .webp, and .gif images are allowed.", nameof(image));
            }

            if (!string.IsNullOrWhiteSpace(image.ContentType) &&
                !image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Uploaded file must be an image.", nameof(image));
            }

            var imagesDirectory = EnsureImagesDirectory();
            var sanitizedName = SanitizeFileName(Path.GetFileNameWithoutExtension(image.FileName));
            var normalizedExtension = extension.ToLowerInvariant();
            var fileName = $"game-{gameId}-{sanitizedName}-{Guid.NewGuid():N}{normalizedExtension}";
            var filePath = Path.Combine(imagesDirectory, fileName);

            try
            {
                await using var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await image.CopyToAsync(stream, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                throw new InvalidOperationException("Game image storage is unavailable.", exception);
            }

            return $"/images/{fileName}";
        }

        public Task DeleteImageAsync(string? imageUrl, CancellationToken cancellationToken = default)
        {
            var imagePath = TryResolveStoredImagePath(imageUrl);
            if (imagePath == null || !File.Exists(imagePath))
            {
                return Task.CompletedTask;
            }

            try
            {
                File.Delete(imagePath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return Task.CompletedTask;
        }

        private string EnsureImagesDirectory()
        {
            var imagesDirectory = Path.Combine(ResolveWebRootPath(), "images");
            Directory.CreateDirectory(imagesDirectory);
            return imagesDirectory;
        }

        private string ResolveWebRootPath()
        {
            if (!string.IsNullOrWhiteSpace(_webHostEnvironment.WebRootPath))
            {
                return _webHostEnvironment.WebRootPath;
            }

            return Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot");
        }

        private string? TryResolveStoredImagePath(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return null;
            }

            var trimmedImageUrl = imageUrl.Trim();
            var relativePath = Uri.TryCreate(trimmedImageUrl, UriKind.Absolute, out var absoluteUri)
                ? absoluteUri.AbsolutePath.TrimStart('/')
                : trimmedImageUrl.TrimStart('~').TrimStart('/');

            relativePath = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            var webRootPath = ResolveWebRootPath();
            var imagesRoot = Path.GetFullPath(Path.Combine(webRootPath, "images"));
            var candidatePath = Path.GetFullPath(Path.Combine(webRootPath, relativePath));
            var imagesPrefix = imagesRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            return candidatePath.StartsWith(imagesPrefix, StringComparison.OrdinalIgnoreCase)
                ? candidatePath
                : null;
        }

        private static string SanitizeFileName(string fileNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                return "game-image";
            }

            var builder = new StringBuilder(fileNameWithoutExtension.Length);
            foreach (var currentCharacter in fileNameWithoutExtension.Trim().ToLowerInvariant())
            {
                var isLowercaseLetter = currentCharacter >= 'a' && currentCharacter <= 'z';
                var isDigit = currentCharacter >= '0' && currentCharacter <= '9';

                if (isLowercaseLetter || isDigit)
                {
                    builder.Append(currentCharacter);
                }
                else if (builder.Length == 0 || builder[^1] != '-')
                {
                    builder.Append('-');
                }
            }

            var sanitized = builder.ToString().Trim('-');
            return string.IsNullOrWhiteSpace(sanitized)
                ? "game-image"
                : sanitized;
        }
    }
}
