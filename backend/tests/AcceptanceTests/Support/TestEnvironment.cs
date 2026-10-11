using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
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
