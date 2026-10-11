namespace Application.Persistence;

internal static class TransactionRunnerExtensions
{
    /// <summary><see cref="ITransactionRunner.RunAsync{T}" /> for work that returns nothing.</summary>
    public static Task RunAsync(
        this ITransactionRunner transactions,
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken)
    {
        return transactions.RunAsync(
            async token =>
            {
                await work(token).ConfigureAwait(false);
                return true;
            },
            cancellationToken);
    }
}
