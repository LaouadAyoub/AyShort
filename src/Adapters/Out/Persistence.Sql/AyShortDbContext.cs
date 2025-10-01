using Microsoft.EntityFrameworkCore;
using Core.Domain.Entities;

namespace Adapters.Out.Persistence.Sql;

public sealed class AyShortDbContext : DbContext
{
    public AyShortDbContext(DbContextOptions<AyShortDbContext> options) : base(options)
    {
    }

    public DbSet<ShortUrl> Links { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure the ShortUrl entity
        var entity = modelBuilder.Entity<ShortUrl>();

        // Table name
        entity.ToTable("Links");

        // Primary key - we'll use Code as the primary key since it's unique
        entity.HasKey(x => x.Code);

        // Configure Code property (ShortCode value object)
        entity.Property(x => x.Code)
            .HasConversion(
                code => code.Value,  // Convert from ShortCode to string
                value => Core.Domain.ValueObjects.ShortCode.Create(value)) // Convert from string to ShortCode
            .HasMaxLength(50)
            .IsRequired();

        // Configure OriginalUrl property (OriginalUrl value object)
        entity.Property(x => x.OriginalUrl)
            .HasConversion(
                url => url.Value,   // Convert from OriginalUrl to string
                value => Core.Domain.ValueObjects.OriginalUrl.Create(value)) // Convert from string to OriginalUrl
            .HasMaxLength(2048)
            .IsRequired();

        // Configure CreatedAt
        entity.Property(x => x.CreatedAt)
            .IsRequired();

        // Configure Expiration (nullable)
        entity.Property(x => x.Expiration);

        // Configure ClicksCount
        entity.Property(x => x.ClicksCount)
            .IsRequired()
            .HasDefaultValue(0);

        // Configure LastAccessAt (nullable)
        entity.Property(x => x.LastAccessAt);

        // Create unique index on Code for fast lookups
        entity.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("IX_Links_Code");

        // Create index on CreatedAt for analytics queries
        entity.HasIndex(x => x.CreatedAt)
            .HasDatabaseName("IX_Links_CreatedAt");
    }
}