namespace Tokito.DTOs.UserDTOs
{
    public class UserRegistrationDTO
    {
        public string UserNickname { get; set; } = string.Empty;
        public string Email { get; set; }
        public string Password { get; set; } = string.Empty;
        public string CountryCode { get; set; } = null!;
    }
}
