using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace WebApi.Telemetry;

internal static class TelemetryExtensions
{
    public static IHostApplicationBuilder AddTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        });

        // Database spans and metrics come from the Aspire Npgsql integration (AddInfrastructure).
        var telemetry = builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation(options =>
                // Health probes would drown the requests that matter.
                options.Filter = context =>
                    !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)))
            .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation());

        // Aspire sets the endpoint; without it (tests, a bare dotnet run) nothing is exported.
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }
}
