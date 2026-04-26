using Microsoft.Extensions.DependencyInjection;

namespace GloryCafe.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
