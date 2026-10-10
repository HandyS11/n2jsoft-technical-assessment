using Microsoft.Playwright;

namespace EndToEndTests.Support;

public abstract class PageTest(AppFixture fixture) : IAsyncLifetime
{
    private static readonly string ArtifactsDirectory =
        Path.Combine(AppContext.BaseDirectory, "playwright-artifacts");

    private IBrowserContext? _context;
    private IPage? _page;

    protected AppFixture Fixture { get; } = fixture;

    protected IPage Page => _page ?? throw new InvalidOperationException("The page is not open.");

    public async ValueTask InitializeAsync()
    {
        _context = await Fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Fixture.AngularUrl.ToString(),
        });
        await _context.Tracing.StartAsync(new TracingStartOptions
        {
            Screenshots = true, Snapshots = true, Sources = true
        });
        _page = await _context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is not null)
        {
            // TestState is already set here; TestStatus still says "Running" and cannot be used.
            if (TestContext.Current.TestState?.Result == TestResult.Failed && _page is not null)
            {
                await SaveFailureArtifactsAsync(_context, _page);
            }
            else
            {
                await _context.Tracing.StopAsync();
            }

            await _context.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    private static async Task SaveFailureArtifactsAsync(IBrowserContext context, IPage page)
    {
        Directory.CreateDirectory(ArtifactsDirectory);
        var name = ArtifactName(TestContext.Current.Test?.TestDisplayName);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(ArtifactsDirectory, $"{name}.png"), FullPage = true,
        });
        await context.Tracing.StopAsync(new TracingStopOptions
        {
            Path = Path.Combine(ArtifactsDirectory, $"{name}.zip")
        });
    }

    private static string ArtifactName(string? testName)
    {
        return string.Concat((testName ?? Guid.NewGuid().ToString("N"))
            .Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));
    }
}
