using Microsoft.AspNetCore.Identity;
using Tokito.DTOs.UserDTOs;

namespace Tokito.Services.Auth
{
    public interface IAuthService
    {
        Task<IdentityResult> RegisterUserAsync(UserRegistrationDTO dto);

        Task<IdentityResult> RegisterPublisherAsync(PublisherRegistrationDTO dto);

        Task<SignInResult> LoginAsync(UserLoginDTO dto);

        Task<UserProfileDto> GetProfileAsync(int userId);
        Task<IEnumerable<UserProfileDto>> GetAllProfilesAsync();

        Task<ChangeCountryResultDto> ChangeCountryAsync(int userId, ChangeCountryDto dto);

        Task UpdateAccountStatusAsync(int userId, byte accountStatus);

        Task<DeleteUserResultDto> DeleteUserAsync(int userId);

        Task LogoutAsync();
    }
}
