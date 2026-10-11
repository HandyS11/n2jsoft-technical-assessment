using Application;
using Asp.Versioning;
using Infrastructure;
using Scalar.AspNetCore;
using WebApi.Endpoints;
using WebApi.Errors;

var builder = WebApplication.CreateBuilder(args);

builder.AddInfrastructure();
builder.Services.AddApplication();
builder.Services.AddErrorHandling(builder.Environment);
builder.Services.AddValidation();
builder.Services.AddHealthChecks();
// 1.0 is the default API version. This AddOpenApi() replaces services.AddOpenApi(): one document per version.
builder.Services
    .AddApiVersioning(options =>
    {
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    })
    .AddOpenApi();

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
        // Exit code 1, not 0: the Aspire dashboard and the end-to-end fixture then report a failure, not a clean stop.
        Environment.ExitCode = 1;
        return;
    }

    app.MapOpenApi().WithDocumentPerVersion();
    app.MapScalarApiReference(options =>
    {
        foreach (var description in app.DescribeApiVersions())
        {
            options.AddDocument(description.GroupName);
        }
    });
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseHttpsRedirection();

app.MapUserEndpoints();
app.MapExpenseReportEndpoints();
app.MapHealthChecks("/health");

await app.RunAsync().ConfigureAwait(false);
