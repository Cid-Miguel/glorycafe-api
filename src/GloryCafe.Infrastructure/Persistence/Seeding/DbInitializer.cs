using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GloryCafe.Infrastructure.Persistence.Seeding;

public class DbInitializer
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly AdminSeedSettings _seed;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(
        ApplicationDbContext db,
        IPasswordHasher hasher,
        IOptions<AdminSeedSettings> seed,
        ILogger<DbInitializer> logger)
    {
        _db = db;
        _hasher = hasher;
        _seed = seed.Value;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.MigrateAsync(cancellationToken);
        await SeedAdminAsync(cancellationToken);
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_seed.Email) || string.IsNullOrWhiteSpace(_seed.Password))
        {
            _logger.LogWarning("AdminSeed not configured — skipping admin seed.");
            return;
        }

        var email = _seed.Email.Trim().ToLowerInvariant();
        var exists = await _db.AdminUsers.AnyAsync(u => u.Email == email, cancellationToken);
        if (exists)
            return;

        var admin = new AdminUser
        {
            Email = email,
            DisplayName = _seed.DisplayName,
            PasswordHash = _hasher.Hash(_seed.Password),
            CreatedAt = DateTime.UtcNow
        };

        _db.AdminUsers.Add(admin);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded initial admin user '{Email}'.", email);
    }
}

public static class DbInitializerExtensions
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<DbInitializer>();
        await initializer.InitializeAsync(cancellationToken);
    }
}
