using Microsoft.EntityFrameworkCore;
using Core.Application.Ports.Out;
using Core.Domain.Entities;
using Core.Domain.Exceptions;
using Npgsql;

namespace Adapters.Out.Persistence.Sql;

public sealed class SqlShortUrlRepository : IShortUrlRepository
{
    private readonly AyShortDbContext _context;

    public SqlShortUrlRepository(AyShortDbContext context)
    {
        _context = context;
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
    {
        try
        {
            // Use FromSqlRaw to query the database directly with string comparison
            return await _context.Links
                .FromSqlRaw("SELECT * FROM \"Links\" WHERE \"Code\" = {0}", code)
                .AsNoTracking()
                .AnyAsync(ct);
        }
        catch (Exception ex)
        {
            throw new InfrastructureException($"Failed to check if code exists: {code}", ex);
        }
    }

    public async Task AddAsync(ShortUrl link, CancellationToken ct = default)
    {
        try
        {
            _context.Links.Add(link);
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // This handles the case where the same code is generated concurrently
            throw new ConflictException($"Short code '{link.Code.Value}' already exists.");
        }
        catch (Exception ex)
        {
            throw new InfrastructureException($"Failed to add short URL with code: {link.Code.Value}", ex);
        }
    }

    public async Task<ShortUrl?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        try
        {
            // Use FromSqlRaw to query the database directly with string comparison
            return await _context.Links
                .FromSqlRaw("SELECT * FROM \"Links\" WHERE \"Code\" = {0}", code)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);
        }
        catch (Exception ex)
        {
            throw new InfrastructureException($"Failed to get short URL by code: {code}", ex);
        }
    }

    public async Task UpdateAsync(ShortUrl link, CancellationToken ct = default)
    {
        try
        {
            _context.Links.Update(link);
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // This handles the case where the entity was modified by another process
            throw new ConflictException($"Short URL with code '{link.Code.Value}' was modified by another process.", ex);
        }
        catch (Exception ex)
        {
            throw new InfrastructureException($"Failed to update short URL with code: {link.Code.Value}", ex);
        }
    }

    /// <summary>
    /// Determines if the exception is due to a unique constraint violation
    /// </summary>
    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // PostgreSQL unique constraint violation error code is 23505
        return ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505";
    }
}