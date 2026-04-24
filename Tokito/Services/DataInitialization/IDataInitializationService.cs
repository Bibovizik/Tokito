using Tokito.DTOs.AdminDTOs;

namespace Tokito.Services.DataInitialization
{
    public interface IDataInitializationService
    {
        Task<DataInitializationResultDto> InitializeAsync(
            DataInitializationRequestDto request,
            CancellationToken cancellationToken = default);
    }
}
