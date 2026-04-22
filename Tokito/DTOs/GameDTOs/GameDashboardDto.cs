namespace Tokito.DTOs.GameDTOs;

public class GameDashboardDto
{
    public DateOnly DateFrom { get; set; }

    public DateOnly DateTo { get; set; }

    public int? PublisherId { get; set; }

    public int? GameId { get; set; }

    public GameDashboardTotalsDto Totals { get; set; } = new();

    public IReadOnlyCollection<GameDashboardGameSummaryDto> Games { get; set; } = Array.Empty<GameDashboardGameSummaryDto>();

    public IReadOnlyCollection<GameDashboardDailyPointDto> Daily { get; set; } = Array.Empty<GameDashboardDailyPointDto>();

    public GameDashboardAdminOverviewDto? AdminOverview { get; set; }
}

public class GameDashboardTotalsDto
{
    public decimal RevenueUah { get; set; }

    public int CopiesSold { get; set; }

    public int GameCount { get; set; }
}

public class GameDashboardGameSummaryDto
{
    public int GameId { get; set; }

    public string GameName { get; set; } = string.Empty;

    public decimal RevenueUah { get; set; }

    public int CopiesSold { get; set; }
}

public class GameDashboardDailyPointDto
{
    public DateOnly Date { get; set; }

    public decimal RevenueUah { get; set; }

    public int CopiesSold { get; set; }
}

public class GameDashboardAdminOverviewDto
{
    public int TotalUsers { get; set; }

    public int ActiveUsers { get; set; }

    public int BlockedUsers { get; set; }

    public int TotalPublishers { get; set; }

    public int TotalGames { get; set; }

    public int TotalReviews { get; set; }

    public int TotalPurchases { get; set; }

    public int TotalLibraryEntries { get; set; }
}
