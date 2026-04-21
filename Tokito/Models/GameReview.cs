using System;
using System.ComponentModel.DataAnnotations;

namespace Tokito.Models;

public class GameReview
{
    public int UserId { get; set; }
    public virtual User User { get; set; } = null!;

    public int GameId { get; set; }
    public virtual Game Game { get; set; } = null!;

    [Range(1, 10)]
    public int Score { get; set; }

    [MaxLength(1000)]
    public string? Review { get; set; }
    public DateTime RatedAt { get; set; } = DateTime.UtcNow;
}