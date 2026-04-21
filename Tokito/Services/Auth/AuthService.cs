using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Tokito.DTOs.UserDTOs;
using Tokito.Models;

namespace Tokito.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IConfiguration _configuration;
        public AuthService(UserManager<User> userManager, IConfiguration configuration, SignInManager<User> signInManager)
        {
            _userManager = userManager;
            _configuration = configuration;
            _signInManager = signInManager;
        }

        public async Task<IdentityResult> RegisterUserAsync(UserRegistrationDTO dto)
        {
            var user = new User
            {
                UserName = dto.UserNickname,
                UserNickname = dto.UserNickname,
                Email = dto.Email,
                RegistrationDate = DateOnly.FromDateTime(DateTime.Now),
                AccountStatus = 1
            };
            
            var result = await _userManager.CreateAsync(user, dto.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");
            }
            return result;
        }

        public async Task<Microsoft.AspNetCore.Identity.SignInResult> LoginAsync(UserLoginDTO dto)
        {
            var result = await _signInManager.PasswordSignInAsync(
                dto.Email,
                dto.Password,
                isPersistent: false, 
                lockoutOnFailure: false);

            return result;
        }

        public async Task LogoutAsync()
        {
            await _signInManager.SignOutAsync();
        }
    }
}
