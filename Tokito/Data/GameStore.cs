using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
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


    public virtual DbSet<Game> Games { get; set; }

    public virtual DbSet<Genre> Genres { get; set; }

    public virtual DbSet<Publisher> Publishers { get; set; }

    public virtual DbSet<Region> Regions { get; set; }

    public virtual DbSet<RegionCountry> RegionCountries { get; set; }

    public virtual DbSet<RegionalPrice> RegionalPrices { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<WalletBalance> WalletBalances { get; set; }

    public virtual DbSet<WalletEntry> WalletEntries { get; set; }

    public virtual DbSet<GameReview> GameReviews { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.Property(e => e.CountryCode)
                .HasMaxLength(3);

            entity.HasMany(e => e.Games)
                .WithMany(g => g.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserLibrary",
                    right => right.HasOne<Game>().WithMany().HasForeignKey("GameId"),
                    left => left.HasOne<User>().WithMany().HasForeignKey("UserId"),
                    join => join.HasKey("UserId", "GameId")
                );
        });

        modelBuilder.Entity<Publisher>(entity =>
        {
            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<Publisher>(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.Property(e => e.BasePriceUah)
                .HasColumnType("decimal(18,2)");

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
            entity.Property(e => e.AmountPaid)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.BasePriceUahSnapshot)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.ExchangeRateSnapshot)
                .HasColumnType("decimal(18,6)");

            entity.Property(e => e.CurrencyCode)
                .HasMaxLength(3);

            entity.Property(e => e.PriceSource)
                .HasMaxLength(32);

            entity.HasOne(e => e.User)
                .WithMany(p => p.Transactions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Game)
                .WithMany(g => g.Transactions)
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Region)
                .WithMany()
                .HasForeignKey(e => e.RegionId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<RegionalPrice>(entity =>
        {
            entity.HasKey(e => e.PriceId);

            entity.HasIndex(e => new { e.GameId, e.RegionId })
                .IsUnique();

            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.PriceSource)
                .HasMaxLength(32);

            entity.Property(e => e.ExchangeRateToUahSnapshot)
                .HasColumnType("decimal(18,6)");

            entity.Property(e => e.ExchangeDate)
                .HasColumnType("date");

            entity.HasOne(e => e.Game)
                .WithMany(g => g.RegionalPrices)
                .HasForeignKey(e => e.GameId);

            entity.HasOne(e => e.Region)
                .WithMany(r => r.RegionalPrices)
                .HasForeignKey(e => e.RegionId);
        });

        modelBuilder.Entity<Region>(entity =>
        {
            entity.HasIndex(e => e.Code)
                .IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(16);

            entity.Property(e => e.CurrencyCode)
                .HasMaxLength(3);

            entity.Property(e => e.CurrencySymbol)
                .HasMaxLength(8);

            entity.Property(e => e.Name)
                .HasMaxLength(100);
        });

        modelBuilder.Entity<RegionCountry>(entity =>
        {
            entity.HasKey(e => e.CountryCode);

            entity.Property(e => e.CountryCode)
                .HasMaxLength(3);

            entity.HasOne(e => e.Region)
                .WithMany(r => r.Countries)
                .HasForeignKey(e => e.RegionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WalletBalance>(entity =>
        {
            entity.HasKey(e => e.WalletBalanceId);

            entity.HasIndex(e => e.UserId)
                .IsUnique();

            entity.Property(e => e.AvailableAmount)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.RowVersion)
                .IsRowVersion();

            entity.HasOne(e => e.User)
                .WithMany(u => u.WalletBalances)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WalletEntry>(entity =>
        {
            entity.HasKey(e => e.WalletEntryId);

            entity.Property(e => e.CurrencyCode)
                .HasMaxLength(3);

            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.BalanceAfter)
                .HasColumnType("decimal(18,2)");

            entity.Property(e => e.Description)
                .HasMaxLength(200);

            entity.Property(e => e.ExchangeRateToUahSnapshot)
                .HasColumnType("decimal(18,6)");

            entity.Property(e => e.AmountUahSnapshot)
                .HasColumnType("decimal(18,2)");

            entity.HasOne(e => e.User)
                .WithMany(u => u.WalletEntries)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Transaction)
                .WithMany()
                .HasForeignKey(e => e.TransactionId)
                .OnDelete(DeleteBehavior.NoAction);
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
        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
