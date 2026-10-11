using Application.Paging;
using Domain.Entities;

namespace Application.Users;

/// <summary>
/// The users feature's data access, implemented over the DbContext (which already is the unit of work). Deleted
/// users are invisible to every member.
/// </summary>
public interface IUserStore
{
    /// <summary>Every user when <paramref name="isActive" /> is null, otherwise only the active or the inactive ones.</summary>
    Task<PagedResult<UserSummary>> ListAsync(PageRequest request, bool? isActive, CancellationToken cancellationToken);

    Task<UserDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The tracked user, to change through its domain methods; null when unknown or deleted.</summary>
    Task<User?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// <see cref="FindAsync" />, but the user stays locked until the transaction ends: a change to it saved by another
    /// request waits until then, and one saved before is already seen. Only within
    /// <see cref="Persistence.ITransactionRunner.RunAsync{T}" />.
    /// </summary>
    Task<User?> FindLockedAsync(Guid id, CancellationToken cancellationToken);

    void Add(User user);

    /// <summary>
    /// Saves every change of the request, whatever store made it (they share one DbContext); throws
    /// <see cref="Exceptions.ConcurrencyConflictException" /> when a changed user was saved by another request since
    /// it was read. A report's save fails as <see cref="ExpenseReports.IExpenseReportStore.SaveChangesAsync" /> says.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
