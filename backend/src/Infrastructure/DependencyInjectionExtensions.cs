using Application.ExpenseReports;
using Application.Persistence;
using Application.Users;
using Infrastructure.ExpenseReports;
using Infrastructure.Persistence;
using Infrastructure.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure;

public static class DependencyInjectionExtensions
{
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<AppDbContext>(connectionName: "database",
            configureDbContextOptions: options =>
                options.UseAsyncSeeding(async (context, _, cancellationToken) =>
                    await AppDbContext.SeedAsync(context, cancellationToken).ConfigureAwait(false)));

        builder.Services.AddScoped<ITransactionRunner, TransactionRunner>();
        builder.Services.AddScoped<IUserStore, UserStore>();
        builder.Services.AddScoped<IExpenseReportStore, ExpenseReportStore>();

        return builder;
    }

    public static async Task MigrateDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
