using System.Diagnostics.Tracing;

namespace AcceptanceTests.Support;

/// <summary>Counts the failed exports the OTLP exporter reports through its event source.</summary>
public sealed class OtlpExportFailures : EventListener
{
    private const string ExporterEventSource = "OpenTelemetry-Exporter-OpenTelemetryProtocol";

    /// <summary>
    /// The events of an export that did not go through. A refused connection is a TransientHttpError, a warning; the
    /// exporter's other warnings (an invalid setting, an attribute it drops) are not failed exports.
    /// </summary>
    private static readonly HashSet<string> FailedExportEvents =
    [
        "FailedToReachCollector", "TransientHttpError", "HttpRequestFailed", "RequestTimedOut",
        "OperationUnexpectedlyCanceled", "ExportFailure", "ExportMethodException",
    ];

    private int _count;

    public int Count => Volatile.Read(ref _count);

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == ExporterEventSource)
        {
            EnableEvents(eventSource, EventLevel.Warning);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventName is { } name && FailedExportEvents.Contains(name))
        {
            Interlocked.Increment(ref _count);
        }
    }
}
