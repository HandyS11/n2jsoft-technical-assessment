using System.Diagnostics;

namespace AcceptanceTests.Support;

/// <summary>For what the API does in the background: a step polls until it is done.</summary>
public static class Polling
{
    public static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string failure)
    {
        var waited = Stopwatch.StartNew();
        while (!condition())
        {
            if (waited.Elapsed > timeout)
            {
                throw new ShouldAssertException($"{failure} within {timeout}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
    }

    public static async Task<T> WaitForAsync<T>(Func<T?> probe, TimeSpan timeout, string failure)
        where T : class
    {
        T? found = null;
        await WaitUntilAsync(() =>
        {
            found = probe();
            return found is not null;
        }, timeout, failure);
        return found!;
    }
}
