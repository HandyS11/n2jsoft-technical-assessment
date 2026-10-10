using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Playwright;

namespace EndToEndTests.Support;

public sealed class AppFixture : IAsyncLifetime
{
    /// <summary>The first run installs the frontend's npm packages and Chromium: allow plenty of time.</summary>
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    private Uri? _angularUrl;

    private DistributedApplication? _app;
    private IBrowser? _browser;
    private IPlaywright? _playwright;

    public DistributedApplication App => _app ?? throw NotStarted();

    public IBrowser Browser => _browser ?? throw NotStarted();

    public Uri AngularUrl => _angularUrl ?? throw NotStarted();

    public async ValueTask InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(StartupTimeout);
        try
        {
            await StartAsync(timeout.Token);
        }
        catch
        {
            // xUnit does not dispose a fixture whose initialization failed: stop what already started.
            await DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private async Task StartAsync(CancellationToken cancellationToken)
    {
        InstallChromium();

        var builder =
            await DistributedApplicationTestingBuilder.CreateAsync<Projects.N2JSoft_Aspire>(cancellationToken);
        _app = await builder.BuildAsync(cancellationToken);
        await _app.StartAsync(cancellationToken);
        await _app.ResourceNotifications.WaitForResourceAsync("webapi", KnownResourceStates.Running, cancellationToken);
        await _app.ResourceNotifications.WaitForResourceAsync("angular", KnownResourceStates.Running,
            cancellationToken);

        _angularUrl = _app.GetEndpoint("angular", "http");
        await WaitForApiThroughAngularAsync(_angularUrl, cancellationToken);

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
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

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
    }

    private static InvalidOperationException NotStarted()
    {
        return new InvalidOperationException("The application is not started.");
    }
}
