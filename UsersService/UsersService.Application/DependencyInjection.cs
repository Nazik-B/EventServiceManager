using Microsoft.Extensions.DependencyInjection;
using UsersService.Application.Services;

namespace UsersService.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}