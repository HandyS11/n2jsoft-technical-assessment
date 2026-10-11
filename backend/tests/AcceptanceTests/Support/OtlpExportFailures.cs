using System.Diagnostics.Tracing;

namespace AcceptanceTests.Support;

/// <summary>Counts the failures the OTLP exporter reports through its event source.</summary>
public sealed class OtlpExportFailures : EventListener
{
    private const string ExporterEventSource = "OpenTelemetry-Exporter-OpenTelemetryProtocol";

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
        if (eventData.Level <= EventLevel.Warning)
        {
            Interlocked.Increment(ref _count);
        }
    }
}
