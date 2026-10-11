using Application.Paging;

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
}
