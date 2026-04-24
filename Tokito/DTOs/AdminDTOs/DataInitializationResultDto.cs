namespace Tokito.DTOs.AdminDTOs
{
    public class DataInitializationResultDto
    {
        public string SeedFilePath { get; set; } = string.Empty;

        public int GenresCreated { get; set; }

        public int PublisherUsersCreated { get; set; }

        public int PublishersCreated { get; set; }

        public int GamesCreated { get; set; }

        public int PlayerUsersCreated { get; set; }

        public int WalletsSeeded { get; set; }

        public int TransactionsCreated { get; set; }

        public int ReviewsCreated { get; set; }

        public int SkippedExistingPublisherUsers { get; set; }

        public int SkippedExistingPublishers { get; set; }

        public int SkippedExistingGames { get; set; }

        public int SkippedExistingPlayerUsers { get; set; }
    }
}
