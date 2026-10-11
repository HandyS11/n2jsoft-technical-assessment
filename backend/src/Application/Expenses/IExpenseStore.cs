using Application.Paging;
using Domain.Entities;

namespace Application.Expenses;

/// <summary>
/// The expenses feature's data access, implemented over the DbContext (which already is the unit of work). Deleted
/// expenses are invisible to every member, and an expense is only found through its own report.
/// </summary>
public interface IExpenseStore
{
    /// <summary>By date, then in creation order.</summary>
    Task<PagedResult<ExpenseSummary>> ListAsync(
        Guid reportId,
        PageRequest request,
        CancellationToken cancellationToken);

    Task<ExpenseDetails?> GetDetailsAsync(Guid reportId, Guid id, CancellationToken cancellationToken);

    /// <summary>The report's expenses not deleted: what the monthly quota counts.</summary>
    Task<int> CountLiveAsync(Guid reportId, CancellationToken cancellationToken);

    /// <summary>The tracked expense, to change through its domain methods; null when unknown, deleted or in another report.</summary>
    Task<Expense?> FindAsync(Guid reportId, Guid id, CancellationToken cancellationToken);

    void Add(Expense expense);

    /// <summary>
    /// Saves every change of the request, whatever store made it (they share one DbContext); throws
    /// <see cref="Exceptions.ConcurrencyConflictException" /> when a changed expense was saved by another request since
    /// it was read.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
