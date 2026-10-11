using Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

internal sealed class TransactionRunner(AppDbContext dbContext) : ITransactionRunner
{
    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        // The execution strategy retries transient failures; inside a transaction it can only retry all of it.
        return dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            async token =>
            {
                // A retry must not save the entities a failed attempt added.
                dbContext.ChangeTracker.Clear();
                var transaction = await dbContext.Database.BeginTransactionAsync(token).ConfigureAwait(false);
                await using (transaction.ConfigureAwait(false))
                {
                    var result = await work(token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return result;
                }
            },
            cancellationToken);
    }
}
