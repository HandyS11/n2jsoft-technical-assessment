using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace WebApi.Telemetry;

internal static class TelemetryExtensions
{
    public static IHostApplicationBuilder AddTelemetry(this IHostApplicationBuilder builder)
    {
        // Traced even without an exporter: a host can add its own (the acceptance tests record the spans in memory).
        // Database spans and metrics come from the Aspire Npgsql integration (AddInfrastructure).
        var telemetry = builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation(options =>
                // Health probes would drown the requests that matter.
                options.Filter = context =>
                    !context.Request.Path.StartsWithSegments(HealthProbes.Path, StringComparison.OrdinalIgnoreCase)));

        // Aspire sets the endpoint; without it (tests, a bare dotnet run) nothing is exported, so logs and metrics
        // are not collected either.
        if (string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            return builder;
        }

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        });
        telemetry
            .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation())
            .UseOtlpExporter();

        return builder;
    }
}
