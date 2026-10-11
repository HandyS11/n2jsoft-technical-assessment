using System.Diagnostics;
using AcceptanceTests.Support;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

/// <summary>
/// Server spans are found by their path: their <c>http.route</c> is the route template
/// (<c>/v{version:apiVersion}/users/</c>), the same for every version.
/// </summary>
[Binding]
public sealed class TelemetrySteps(ApiDriver api)
{
    /// <summary>The server span ends once the response is written, possibly after the client has read it.</summary>
    private static readonly TimeSpan SpanTimeout = TimeSpan.FromSeconds(5);

    [Given("the API records its traces")]
    public void GivenTheApiRecordsItsTraces()
    {
        TestEnvironment.RecordedSpans.Clear();
        api.UseTelemetryRecording();
    }

    [Given("the API exports its telemetry to a collector that does not answer")]
    public void GivenTheApiExportsItsTelemetryToACollectorThatDoesNotAnswer()
    {
        api.UseUnreachableCollector();
    }

    [Then("the request to {string} is traced as a server span with the status {int}")]
    public static async Task ThenTheRequestIsTracedAsAServerSpanAsync(string path, int status)
    {
        var span = await WaitForServerSpanAsync(path);

        span.GetTagItem("http.response.status_code").ShouldBe(status);
    }

    [Then("the trace of the request to {string} contains a database span")]
    public static async Task ThenTheTraceContainsADatabaseSpanAsync(string path)
    {
        var span = await WaitForServerSpanAsync(path);

        TestEnvironment.RecordedSpans.Snapshot()
            .ShouldContain(other => other.TraceId == span.TraceId && other.Source.Name == "Npgsql",
                $"the trace of {path} has no Npgsql span");
    }

    /// <summary>Run after waiting for a later request's span: the earlier request's span would have ended by then.</summary>
    [Then("no server span is recorded for the path {string}")]
    public static void ThenNoServerSpanIsRecordedForThePath(string path)
    {
        TestEnvironment.RecordedSpans.Snapshot().ShouldNotContain(span => IsServerSpanFor(span, path));
    }

    private static async Task<Activity> WaitForServerSpanAsync(string path)
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            var span = Array.Find(TestEnvironment.RecordedSpans.Snapshot(), span => IsServerSpanFor(span, path));
            if (span is not null)
            {
                return span;
            }

            if (waited.Elapsed > SpanTimeout)
            {
                throw new ShouldAssertException($"No server span for {path} within {SpanTimeout}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
    }

    private static bool IsServerSpanFor(Activity span, string path)
    {
        return span.Kind == ActivityKind.Server && (string?)span.GetTagItem("url.path") == path;
    }
}
