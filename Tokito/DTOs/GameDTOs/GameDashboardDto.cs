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
