using System.Diagnostics;
using AcceptanceTests.Support;
using Microsoft.AspNetCore.Mvc.Testing;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

/// <summary>
/// Server spans are found by their path: their <c>http.route</c> is the route template
/// (<c>/v{version:apiVersion}/users/</c>), the same for every version.
/// </summary>
[Binding]
public sealed class TelemetrySteps(ApiDriver api) : IDisposable
{
    /// <summary>The server span ends once the response is written, possibly after the client has read it.</summary>
    private static readonly TimeSpan SpanTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Well past the 100 ms export delay and the 1 s request timeout the unreachable-collector factory sets, so a
    /// connection that is refused slowly (about 2 s on Windows) still fails in time.
    /// </summary>
    private static readonly TimeSpan ExportTimeout = TimeSpan.FromSeconds(10);

    private readonly RecordedSpans _spans = [];
    private OtlpExportFailures? _exportFailures;
    private WebApplicationFactory<Program>? _factory;

    /// <summary>
    /// Reqnroll disposes the steps at the end of the scenario. The tracer provider of the factory listens to every host
    /// in the process: it must not outlive the scenario.
    /// </summary>
    public void Dispose()
    {
        _factory?.Dispose();
        _exportFailures?.Dispose();
    }

    [Given("the API records its traces")]
    public void GivenTheApiRecordsItsTraces()
    {
        _factory = TestEnvironment.CreateTelemetryRecordingFactory(_spans);
        api.UseFactory(_factory);
    }

    [Given("the API exports its telemetry to a collector that does not answer")]
    public void GivenTheApiExportsItsTelemetryToACollectorThatDoesNotAnswer()
    {
        _exportFailures = new OtlpExportFailures();
        _factory = TestEnvironment.CreateUnreachableCollectorFactory();
        api.UseFactory(_factory);
    }

    [When("the API has failed to export its telemetry")]
    public Task WhenTheApiHasFailedToExportItsTelemetryAsync()
    {
        var failures = _exportFailures ?? throw new InvalidOperationException("The API exports to no collector.");
        return Polling.WaitUntilAsync(() => failures.Count > 0, ExportTimeout,
            "The OTLP exporter reported no failed export");
    }

    [Then("the request to {string} is traced as a server span with the status {int}")]
    public async Task ThenTheRequestIsTracedAsAServerSpanAsync(string path, int status)
    {
        var span = await WaitForServerSpanAsync(path);

        span.GetTagItem("http.response.status_code").ShouldBe(status);
    }

    [Then("the trace of the request to {string} contains a database span")]
    public async Task ThenTheTraceContainsADatabaseSpanAsync(string path)
    {
        var span = await WaitForServerSpanAsync(path);

        _spans.Snapshot()
            .ShouldContain(other => other.TraceId == span.TraceId && other.Source.Name == "Npgsql",
                $"the trace of {path} has no Npgsql span");
    }

    /// <summary>Run after waiting for a later request's span: the earlier request's span would have ended by then.</summary>
    [Then("no server span is recorded for the path {string}")]
    public void ThenNoServerSpanIsRecordedForThePath(string path)
    {
        _spans.Snapshot().ShouldNotContain(span => IsServerSpanFor(span, path));
    }

    private Task<Activity> WaitForServerSpanAsync(string path)
    {
        return Polling.WaitForAsync(() => Array.Find(_spans.Snapshot(), span => IsServerSpanFor(span, path)),
            SpanTimeout, $"No server span for {path}");
    }

    private static bool IsServerSpanFor(Activity span, string path)
    {
        return span.Kind == ActivityKind.Server && (string?)span.GetTagItem("url.path") == path;
    }
}
