using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Tokito.Data;

namespace Tokito.Models;

public partial class Game
{
    public int GameId { get; set; }

    public string Name { get; set; } = null!;

    public DateOnly? ReleaseDate { get; set; }

    public int? Rating { get; set; }

    public int PublisherId { get; set; }

    public string? SystemRequirements { get; set; }

    public int MostOneTimePlayers { get; set; }

    public string? PublisherName { get; set; }

    public virtual Publisher Publisher { get; set; } = null!;

    [MaxLength(200)]
    public virtual string Desription { get; set; } = string.Empty; 

    public virtual ICollection<Genre> Genres { get; set; } = new List<Genre>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public string? ImageUrl { get; set; }

    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();

    public virtual ICollection<GameReview> GameReviews { get; set; } = new List<GameReview>();

    [Required]
    [Range(0, double.MaxValue)]
    public decimal BasePriceUah { get; set; }
}

