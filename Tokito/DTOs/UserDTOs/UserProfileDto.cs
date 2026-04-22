namespace Tokito.DTOs.UserDTOs
{
    public class UserProfileDto
    {
        public int UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string UserNickname { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string CountryCode { get; set; } = string.Empty;

        public DateOnly? RegistrationDate { get; set; }

        public byte AccountStatusCode { get; set; }

        public string AccountStatus { get; set; } = string.Empty;

        public int? PublisherId { get; set; }

        public string? PublisherName { get; set; }

        public string[] Roles { get; set; } = [];
    }
}
