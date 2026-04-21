using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tokito.DTOs.UserDTOs;
using Tokito.Models;

namespace Tokito.Services.Auth
{
    public interface IAuthService
    {
        Task<IdentityResult> RegisterUserAsync(UserRegistrationDTO dto);
        Task<Microsoft.AspNetCore.Identity.SignInResult> LoginAsync(UserLoginDTO dto);
        Task LogoutAsync();
    }
}
