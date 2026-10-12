using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;

namespace EndToEndTests.Support;

public sealed class AppFixture : IAsyncLifetime
{
    /// <summary>The first run installs the frontend's npm packages and Chromium: allow plenty of time.</summary>
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Bounds teardown, so an application that does not stop cannot hang the test run.</summary>
    private static readonly TimeSpan StopTimeout = TimeSpan.FromMinutes(1);

    private static readonly string[] ResourceNames = ["webapi", "angular"];

    private DistributedApplication? _app;
    private IBrowser? _browser;
    private IPlaywright? _playwright;

    public DistributedApplication App => _app ?? throw NotStarted();

    public IBrowser Browser => _browser ?? throw NotStarted();

    public Uri AngularUrl
    {
        get => field ?? throw NotStarted();
        private set;
    }

    public async ValueTask InitializeAsync()
    {
        try
        {
            await StartWithinTimeoutAsync();
        }
        catch (Exception startupFailure)
        {
            // xUnit does not dispose a fixture whose initialization failed: stop what already started.
            await DisposeAfterFailedStartAsync(startupFailure);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_browser is not null)
            {
                await _browser.DisposeAsync();
            }

            _playwright?.Dispose();
        }
        finally
        {
            // The Postgres container, the webapi and the npm dev server must stop even if the browser did not close.
            await StopAppAsync();
        }
    }

    private async Task DisposeAfterFailedStartAsync(Exception startupFailure)
    {
        try
        {
            await DisposeAsync();
        }
        catch (Exception disposeFailure)
        {
            // Keep the startup failure first: it is the one to fix.
            throw new AggregateException("The application failed to start, then failed to stop.", startupFailure,
                disposeFailure);
        }
    }

    private async Task StopAppAsync()
    {
        if (_app is null)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(StopTimeout);
            await _app.StopAsync(timeout.Token);
        }
        finally
        {
            await _app.DisposeAsync();
        }
    }

    private async Task StartWithinTimeoutAsync()
    {
        using var timeout = new CancellationTokenSource(StartupTimeout);
        try
        {
            await StartAsync(timeout.Token);
        }
        catch (OperationCanceledException e) when (timeout.IsCancellationRequested)
        {
            throw new TimeoutException($"The application was not ready after {StartupTimeout.TotalMinutes} minutes.",
                e);
        }
    }

    private async Task StartAsync(CancellationToken cancellationToken)
    {
        InstallChromium();

        var builder =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.N2JSoft_Aspire>(cancellationToken);
        UseFreshDatabase(builder);
        _app = await builder.BuildAsync(cancellationToken);
        await _app.StartAsync(cancellationToken);
        AngularUrl = await WaitUntilReadyAsync(_app, cancellationToken);

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    /// <summary>
    /// The AppHost keeps the database in a Docker volume between runs. Without it, every test run starts from the
    /// seed, and the tests never read or change the data of a developer's own runs.
    /// </summary>
    private static void UseFreshDatabase(IDistributedApplicationTestingBuilder builder)
    {
        var postgres = builder.Resources.Single(resource => resource.Name == "postgres");
        var volumes = postgres.Annotations.OfType<ContainerMountAnnotation>()
            .Where(mount => mount.Type == ContainerMountType.Volume)
            .ToList();
        foreach (var volume in volumes)
        {
            postgres.Annotations.Remove(volume);
        }
    }

    /// <summary>
    /// Idempotent and fast once Chromium is cached in ~/.cache/ms-playwright, so no manual install step is needed.
    /// </summary>
    private static void InstallChromium()
    {
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Installing Chromium for Playwright failed with exit code {exitCode}.");
        }
    }

    /// <summary>
    /// Waits for the pages to be usable, but fails as soon as the webapi or the Angular dev server stops (failed
    /// startup migration, port conflict, build error) instead of waiting for the whole startup timeout.
    /// </summary>
    private static async Task<Uri> WaitUntilReadyAsync(DistributedApplication app, CancellationToken cancellationToken)
    {
        using var stopWatching = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ready = WaitForPagesAsync(app, stopWatching.Token);
        Task[] readyOrStopped =
            [ready, .. ResourceNames.Select(name => ThrowWhenStoppedAsync(app, name, stopWatching.Token))];

        var first = await Task.WhenAny(readyOrStopped);
        await stopWatching.CancelAsync();
        // Throws if a resource stopped first, or if the startup timeout elapsed.
        await first;
        return await ready;
    }

    private static async Task ThrowWhenStoppedAsync(
        DistributedApplication app,
        string resourceName,
        CancellationToken cancellationToken)
    {
        var stopped = await app.ResourceNotifications.WaitForResourceAsync(resourceName,
            resourceEvent => resourceEvent.Snapshot.State?.Text is { } state &&
                             KnownResourceStates.TerminalStates.Contains(state),
            cancellationToken);
        throw new InvalidOperationException(
            $"The {resourceName} resource stopped ({stopped.Snapshot.State?.Text}, exit code " +
            $"{stopped.Snapshot.ExitCode}) before the application was ready.");
    }

    private static async Task<Uri> WaitForPagesAsync(DistributedApplication app, CancellationToken cancellationToken)
    {
        await app.ResourceNotifications.WaitForResourceAsync("webapi", KnownResourceStates.Running, cancellationToken);
        await app.ResourceNotifications.WaitForResourceAsync("angular", KnownResourceStates.Running,
            cancellationToken);

        var angularUrl = app.GetEndpoint("angular", "http");
        await WaitForApiThroughAngularAsync(angularUrl, cancellationToken);
        return angularUrl;
    }

    /// <summary>
    /// "Running" only means the processes started. The pages are usable once the Angular dev server has built and
    /// the webapi answers through its proxy (startup migration done). The webapi has no Aspire health check: it would
    /// probe the https endpoint, which fails wherever the ASP.NET Core dev certificate is not trusted (CI included).
    /// </summary>
    private static async Task WaitForApiThroughAngularAsync(Uri angularUrl, CancellationToken cancellationToken)
    {
        var usersThroughProxy = new Uri(angularUrl, "api/v1/users");
        using var http = new HttpClient();
        while (true)
        {
            try
            {
                using var response = await http.GetAsync(usersThroughProxy, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // The dev server or the webapi is not listening yet.
            }
            catch (TaskCanceledException e) when (e.InnerException is TimeoutException)
            {
                // HttpClient's timeout: the dev server accepted the request but is still building. Ask again.
            }

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }

    private static InvalidOperationException NotStarted()
    {
        return new InvalidOperationException("The application is not started.");
    }
}
