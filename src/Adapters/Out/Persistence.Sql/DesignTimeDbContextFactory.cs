using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace Adapters.Out.Persistence.Sql;

/// <summary>
/// Design-time factory for creating AyShortDbContext during migrations.
/// This is used by EF Core tools when generating migrations.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AyShortDbContext>
{
    public AyShortDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AyShortDbContext>();

        // Determine environment (DOTNET_ENVIRONMENT preferred, fallback to ASPNETCORE_ENVIRONMENT, then Development)
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                           ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                           ?? "Development";

        // Build configuration from project directory (Directory.GetCurrentDirectory is set to the project when design-time tools run)
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables() // allows ConnectionStrings__Default
            .Build();

        var connectionString = config.GetConnectionString("Default");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Default' not found. Supply it via appsettings.json or environment variable 'ConnectionStrings__Default'.");
        }

        optionsBuilder.UseNpgsql(connectionString);
        return new AyShortDbContext(optionsBuilder.Options);
    }
}