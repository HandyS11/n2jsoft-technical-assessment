using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace EndToEndTests.Support;

public abstract partial class PageTest(AppFixture fixture) : IAsyncLifetime
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
            try
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
            }
            finally
            {
                await _context.DisposeAsync();
            }
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The row holding <paramref name="text" /> in the list shown, paging with « Suivant » from the page shown: a list
    /// shows 20 rows a page, and the demo data fills several.
    /// </summary>
    protected async Task<ILocator> FindRowAsync(string text)
    {
        var row = Page.GetByRole(AriaRole.Row).Filter(new()
        {
            HasText = text
        });
        var pagination = Page.GetByRole(AriaRole.Navigation, new()
        {
            Name = "Pagination"
        });
        var pageLabel = pagination.GetByText(PageLabel());
        var next = pagination.GetByRole(AriaRole.Button, new()
        {
            Name = "Suivant"
        });

        // The bar shows with the first page.
        await Assertions.Expect(pageLabel).ToBeVisibleAsync();
        while (await row.CountAsync() == 0)
        {
            if (!await next.IsEnabledAsync())
            {
                throw new InvalidOperationException($"No row holds \"{text}\", up to the last page.");
            }

            var shown = await pageLabel.TextContentAsync() ?? string.Empty;
            await next.ClickAsync();
            await Assertions.Expect(pageLabel).Not.ToHaveTextAsync(shown);
        }

        return row;
    }

    private static async Task SaveFailureArtifactsAsync(IBrowserContext context, IPage page)
    {
        Directory.CreateDirectory(ArtifactsDirectory);
        var name = ArtifactName(TestContext.Current.Test?.TestDisplayName);
        await TryCaptureAsync("screenshot", () => page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(ArtifactsDirectory, $"{name}.png"), FullPage = true,
        }));
        await TryCaptureAsync("trace", () => context.Tracing.StopAsync(new TracingStopOptions
        {
            Path = Path.Combine(ArtifactsDirectory, $"{name}.zip")
        }));
    }

    /// <summary>
    /// The test already failed. An artifact that cannot be captured (the page crashed or closed) becomes a warning,
    /// so that the real failure is the only error reported.
    /// </summary>
    private static async Task TryCaptureAsync(string artifact, Func<Task> capture)
    {
        try
        {
            await capture();
        }
        catch (PlaywrightException e)
        {
            TestContext.Current.AddWarning($"The {artifact} of the failed test could not be saved: {e.Message}");
        }
    }

    [GeneratedRegex(@"^Page \d+ sur \d+$")]
    private static partial Regex PageLabel();

    private static string ArtifactName(string? testName)
    {
        return string.Concat((testName ?? Guid.NewGuid().ToString("N"))
            .Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));
    }
}
