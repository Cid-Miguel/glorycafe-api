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
        await SeedCatalogAsync(cancellationToken);
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

    private async Task SeedCatalogAsync(CancellationToken cancellationToken)
    {
        if (await _db.Categories.AnyAsync(cancellationToken))
            return;

        var coffee = new Category { Name = "Coffee", DisplayOrder = 1, CreatedAt = DateTime.UtcNow };
        var pastries = new Category { Name = "Pastries", DisplayOrder = 2, CreatedAt = DateTime.UtcNow };
        var coldDrinks = new Category { Name = "Cold Drinks", DisplayOrder = 3, CreatedAt = DateTime.UtcNow };

        _db.Categories.AddRange(coffee, pastries, coldDrinks);

        var products = new[]
        {
            new Product { Name = "Flat White",      Description = "Smooth espresso with velvety steamed milk.", Price = 5.50m, Category = coffee },
            new Product { Name = "Long Black",      Description = "Double shot of espresso topped with hot water.", Price = 5.00m, Category = coffee },
            new Product { Name = "Cappuccino",      Description = "Espresso with steamed milk and a thick foam top.", Price = 5.50m, Category = coffee },
            new Product { Name = "Latte",           Description = "Espresso with plenty of steamed milk and light foam.", Price = 5.50m, Category = coffee },
            new Product { Name = "Croissant",       Description = "Buttery, flaky French-style pastry baked daily.", Price = 6.50m, Category = pastries },
            new Product { Name = "Banana Bread",    Description = "Toasted slice of house-made banana bread with butter.", Price = 5.50m, Category = pastries },
            new Product { Name = "Iced Latte",      Description = "Chilled espresso poured over milk and ice.", Price = 6.00m, Category = coldDrinks },
            new Product { Name = "Sparkling Water", Description = "Bottled sparkling mineral water.", Price = 4.00m, Category = coldDrinks }
        };

        foreach (var product in products)
            product.CreatedAt = DateTime.UtcNow;

        _db.Products.AddRange(products);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded catalog with {CategoryCount} categories and {ProductCount} products.",
            3, products.Length);
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
