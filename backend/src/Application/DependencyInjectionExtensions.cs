using Application.Users.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CreateUserHandler>();
        services.AddScoped<UpdateUserHandler>();
        services.AddScoped<SetUserActivationHandler>();
        services.AddScoped<DeleteUserHandler>();
        return services;
    }
}
