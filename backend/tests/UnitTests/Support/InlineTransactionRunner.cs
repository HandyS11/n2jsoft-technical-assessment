using Application.Persistence;

namespace UnitTests.Support;

/// <summary>Runs the work at once, recording whether a step happens inside it.</summary>
public sealed class InlineTransactionRunner : ITransactionRunner
{
    public bool InTransaction { get; private set; }

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        InTransaction = true;
        try
        {
            return await work(cancellationToken);
        }
        finally
        {
            InTransaction = false;
        }
    }
}
