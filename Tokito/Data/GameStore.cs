using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Numerics;
using Tokito.Models;

namespace Tokito.Data;

public partial class GameStore : IdentityDbContext<User, IdentityRole<int>, int>
{
    public GameStore()
    {
    }

    public GameStore(DbContextOptions<GameStore> options)
        : base(options)
    {
    }


    public virtual DbSet<Country> Countries { get; set; }

    public virtual DbSet<Game> Games { get; set; }

    public virtual DbSet<Genre> Genres { get; set; }

    public virtual DbSet<Platform> Platforms { get; set; }

    public virtual DbSet<Publisher> Publishers { get; set; }

    public virtual DbSet<Region> Regions { get; set; }

    public virtual DbSet<RegionalPrice> RegionalPrices { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<Tag> Tags { get; set; }

    public virtual DbSet<GameReview> GameReviews { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Country>(entity =>
        {
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).ValueGeneratedNever();
            entity.HasOne(e => e.Region)
                .WithMany(r => r.Countries)
                .HasForeignKey(e => e.RegionId);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasOne(e => e.Country)
                .WithMany(c => c.Users)
                .HasForeignKey(e => e.CountryId)
                .HasPrincipalKey(c => c.Code)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(e => e.Games)
                .WithMany(g => g.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserLibrary",
                    right => right.HasOne<Game>().WithMany().HasForeignKey("GameId"),
                    left => left.HasOne<User>().WithMany().HasForeignKey("UserId"),
                    join => join.HasKey("UserId", "GameId")
                );
        });

        modelBuilder.Entity<Platform>(entity =>
        {
            entity.HasOne(e => e.Country)
                .WithMany(c => c.Platforms)
                .HasForeignKey(e => e.CountryId)
                .HasPrincipalKey(c => c.Code)
                .IsRequired(false);
        });

        modelBuilder.Entity<Publisher>(entity =>
        {
            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<Publisher>(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Country)
                .WithMany(c => c.Publishers)
                .HasForeignKey(e => e.CountryId)
                .HasPrincipalKey(c => c.Code)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasOne(e => e.Publisher)
                .WithMany(p => p.Games)
                .HasForeignKey(e => e.PublisherId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(e => e.Genres)
                .WithMany(g => g.Games)
                .UsingEntity<Dictionary<string, object>>(
                    "GameGenre",
                    right => right.HasOne<Genre>().WithMany().HasForeignKey("GenreId"),
                    left => left.HasOne<Game>().WithMany().HasForeignKey("GameId"),
                    join => join.HasKey("GameId", "GenreId")
                );
        });

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasOne(e => e.User)
                .WithMany(p => p.Transactions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Game)
                .WithMany(g => g.Transactions)
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<RegionalPrice>(entity =>
        {
            entity.HasKey(e => e.PriceId);

            entity.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId);

            entity.HasOne(e => e.Platform)
                .WithMany()
                .HasForeignKey(e => e.PlatformId);

            entity.HasOne(e => e.Region)
                .WithMany(r => r.RegionalPrices)
                .HasForeignKey(e => e.RegionId);
        });

        modelBuilder.Entity<GameReview>()
            .HasKey(gr => new { gr.UserId, gr.GameId });

        modelBuilder.Entity<GameReview>()
            .HasOne(gr => gr.User)
            .WithMany(u => u.GameRatings)
            .HasForeignKey(gr => gr.UserId)
            .OnDelete(DeleteBehavior.Cascade); 

        modelBuilder.Entity<GameReview>()
            .HasOne(gr => gr.Game)
            .WithMany(g => g.GameReviews)
            .HasForeignKey(gr => gr.GameId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Game>()
            .HasMany(g => g.Tags)
            .WithMany(t => t.Games)
            .UsingEntity(j => j.ToTable("GameTags"));

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
