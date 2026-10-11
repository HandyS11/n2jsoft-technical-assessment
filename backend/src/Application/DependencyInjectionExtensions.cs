using Application.ExpenseReports.Commands;
using Application.Expenses.Commands;
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
        services.AddScoped<CreateExpenseReportHandler>();
        services.AddScoped<DeleteExpenseReportHandler>();
        services.AddScoped<CreateExpenseHandler>();
        services.AddScoped<UpdateExpenseHandler>();
        services.AddScoped<DeleteExpenseHandler>();
        return services;
    }
}
