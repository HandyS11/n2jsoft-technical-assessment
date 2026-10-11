using System.Collections.ObjectModel;
using System.Diagnostics;

namespace AcceptanceTests.Support;

/// <summary>The exporter adds spans from the server's threads while a step reads them: both go through a lock.</summary>
public sealed class RecordedSpans : Collection<Activity>
{
    private readonly Lock _lock = new();

    public Activity[] Snapshot()
    {
        lock (_lock)
        {
            return [.. Items];
        }
    }

    protected override void InsertItem(int index, Activity item)
    {
        lock (_lock)
        {
            base.InsertItem(index, item);
        }
    }

    protected override void ClearItems()
    {
        lock (_lock)
        {
            base.ClearItems();
        }
    }
}
