using System.Diagnostics;
using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Trace;
using Respawn;
using Testcontainers.PostgreSql;

namespace AcceptanceTests.Support;

public static class TestEnvironment
{
    /// <summary>Same Postgres major as the one Aspire runs (Aspire 13.6.1 default image).</summary>
    private const string PostgresImage = "postgres:18.3";

    private static PostgreSqlContainer? _postgres;
    private static ApiFactory? _factory;
    private static Respawner? _respawner;
    private static WebApplicationFactory<Program>? _developmentFactory;

    public static WebApplicationFactory<Program> Factory =>
        _factory ?? throw new InvalidOperationException("The test environment is not started.");

    /// <summary>
    /// Built on first use: only the API documentation scenarios need the Development pipeline. Its startup
    /// migration is a no-op on the already migrated container; the seed it inserts is wiped before the next scenario.
    /// </summary>
    public static WebApplicationFactory<Program> DevelopmentFactory =>
        _developmentFactory ??= Factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

    private static string ConnectionString =>
        _postgres?.GetConnectionString() ?? throw new InvalidOperationException("Postgres is not started.");

    /// <summary>
    /// Adds an in-memory exporter to the tracing the API sets up itself, so its instrumentation and filters apply as
    /// they do under Aspire. For one scenario: dispose it at its end. A tracer provider listens to every host in the
    /// process, so a factory left running would go on recording the spans of the scenarios after it.
    /// </summary>
    public static WebApplicationFactory<Program> CreateTelemetryRecordingFactory(ICollection<Activity> spans)
    {
        return Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddOpenTelemetry().WithTracing(tracing => tracing.AddInMemoryExporter(spans))));
    }

    /// <summary>
    /// Exports over OTLP, as under Aspire, to a port nothing listens on (the dashboard stopped). Traces and logs are
    /// exported every 100 ms rather than every 5 s, and a request gives up after 1 s rather than 10 s, so a scenario
    /// soon sees an export fail. For one scenario, like <see cref="CreateTelemetryRecordingFactory" />: left running,
    /// it would go on exporting the telemetry of the scenarios after it.
    /// </summary>
    public static WebApplicationFactory<Program> CreateUnreachableCollectorFactory()
    {
        return Factory.WithWebHostBuilder(builder => builder
            .UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:9")
            .UseSetting("OTEL_EXPORTER_OTLP_TIMEOUT", "1000")
            .UseSetting("OTEL_BSP_SCHEDULE_DELAY", "100")
            .UseSetting("OTEL_BLRP_SCHEDULE_DELAY", "100"));
    }

    public static async Task StartAsync()
    {
        _postgres = new PostgreSqlBuilder(PostgresImage).Build();
        await _postgres.StartAsync();

        _factory = new ApiFactory(ConnectionString);
        // Migrating also runs the dev seed; ResetDatabaseAsync wipes it before each scenario.
        await _factory.Services.MigrateDatabaseAsync();

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres, SchemasToInclude = ["public"], TablesToIgnore = ["__EFMigrationsHistory"],
        });
    }

    public static async Task ResetDatabaseAsync()
    {
        if (_respawner is null)
        {
            throw new InvalidOperationException("The test environment is not started.");
        }

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
    }

    /// <summary>A connection of its own, outside the API: to hold a transaction open while the API works.</summary>
    public static async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public static async Task StopAsync()
    {
        if (_developmentFactory is not null)
        {
            await _developmentFactory.DisposeAsync();
        }

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }
}
