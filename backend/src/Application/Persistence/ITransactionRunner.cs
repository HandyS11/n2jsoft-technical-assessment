namespace Application.Persistence;

/// <summary>Runs a handler's reads and its save in one database transaction.</summary>
public interface ITransactionRunner
{
    /// <summary>
    /// Runs <paramref name="work" />, then commits. After a transient failure the transaction is rolled back and the
    /// work runs again from scratch, the changes of the failed attempt forgotten: it must be the first thing the
    /// request does, and read inside everything it decides on.
    /// </summary>
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken);
}
