using Infrastructure;
using WebApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.AddInfrastructure();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    try
    {
        await app.Services.MigrateDatabaseAsync().ConfigureAwait(false);
    }
    catch (Exception e)
    {
        app.Logger.LogError(e, "An error occurred while migrating the database.");
        return;
    }

    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapUserEndpoints();
app.MapHealthChecks("/health");

await app.RunAsync().ConfigureAwait(false);
