using Application.Paging;
using Domain.Entities;
using Domain.ValueObjects;

namespace Application.ExpenseReports;

/// <summary>
/// The expense reports feature's data access, implemented over the DbContext (which already is the unit of work).
/// Reads never join the users, so the report of a deleted user stays visible.
/// </summary>
public interface IExpenseReportStore
{
    /// <summary>The most recent months first, then by title.</summary>
    Task<PagedResult<ExpenseReportSummary>> ListAsync(PageRequest request, CancellationToken cancellationToken);

    Task<ExpenseReportDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid userId, ReportMonth period, CancellationToken cancellationToken);

    /// <summary>The tracked report, to delete; null when unknown.</summary>
    Task<ExpenseReport?> FindAsync(Guid id, CancellationToken cancellationToken);

    void Add(ExpenseReport report);

    void Remove(ExpenseReport report);

    /// <summary>
    /// Saves every change of the request, whatever store made it (they share one DbContext). Throws
    /// <see cref="Domain.Exceptions.DuplicateExpenseReportException" /> when a concurrent request created the same
    /// user's report for the same month first, and <see cref="Exceptions.NotFoundException" /> when a concurrent
    /// request deleted a removed report first. A user's save fails as
    /// <see cref="Users.IUserStore.SaveChangesAsync" /> says.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
