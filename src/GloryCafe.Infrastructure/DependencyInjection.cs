using GloryCafe.Application.Common.Interfaces;
using GloryCafe.Infrastructure.Auth;
using GloryCafe.Infrastructure.Common;
using GloryCafe.Infrastructure.Persistence;
using GloryCafe.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GloryCafe.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Connection string 'DefaultConnection' is missing. " +
                "In development, set it via `dotnet user-secrets set " +
                "\"ConnectionStrings:DefaultConnection\" \"...\"`. In " +
                "production, set the env var ConnectionStrings__DefaultConnection.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<AdminSeedSettings>(configuration.GetSection(AdminSeedSettings.SectionName));

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<IOrderNumberAllocator, PostgresOrderNumberAllocator>();
        services.AddScoped<DbInitializer>();

        return services;
    }
}
