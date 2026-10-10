using Microsoft.EntityFrameworkCore;
using WebApi.Infrastructure.Api;
using WebApi.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddNpgsqlDbContext<AppDbContext>(connectionName: "database",
    configureDbContextOptions: options =>
        options.UseAsyncSeeding(async (context, _, cancellationToken) =>
            await AppDbContext.SeedAsync(context, cancellationToken).ConfigureAwait(false)));
builder.Services
    .AddOpenApi()
    .AddPersistence();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    try
    {
        using var sp = app.Services.CreateScope();
        await sp.ServiceProvider.GetRequiredService<AppDbContext>()
            .Database.MigrateAsync().ConfigureAwait(false);
    }
    catch (Exception e)
    {
        app.Services.GetRequiredService<ILogger<Program>>()
            .LogError(e, "An error occurred while migrating the database.");
        return;
    }

    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapUserEndpoints();
app.MapHealthChecks("/health");

await app.RunAsync().ConfigureAwait(false);
