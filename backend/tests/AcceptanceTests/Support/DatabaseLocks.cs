namespace AcceptanceTests.Support;

public static class DatabaseLocks
{
    /// <summary>Waits until a query of the database waits for a lock (at most 10 seconds).</summary>
    public static Task WaitUntilAQueryWaitsForALockAsync(CancellationToken cancellationToken)
    {
        return WaitUntilQueriesWaitForALockAsync(1, cancellationToken);
    }

    /// <summary>
    /// Waits until <paramref name="count" /> queries of the database wait for a lock (at most 10 seconds). On a
    /// connection of its own: within a transaction, Postgres keeps answering the activity it saw first.
    /// </summary>
    public static async Task WaitUntilQueriesWaitForALockAsync(int count, CancellationToken cancellationToken)
    {
        await using var connection = await TestEnvironment.OpenConnectionAsync();
        await using var query = connection.CreateCommand();
        query.CommandText =
            "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while ((long)(await query.ExecuteScalarAsync(timeout.Token))! < count)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
        }
    }
}
