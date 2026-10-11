using Application.ExpenseReports;
using Application.Paging;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.ExpenseReports;

internal sealed class ExpenseReportStore(AppDbContext dbContext) : IExpenseReportStore
{
    public async Task<PagedResult<ExpenseReportSummary>> ListAsync(
        PageRequest request,
        CancellationToken cancellationToken)
    {
        var reports = dbContext.Set<ExpenseReport>().AsNoTracking();
        var totalCount = await reports.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await reports
            .OrderByDescending(report => report.Year)
            .ThenByDescending(report => report.Month)
            .ThenBy(report => report.Title)
            // Reports sharing a title (two users with the same name) keep the same order from one page to the next.
            .ThenBy(report => report.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(report => new ExpenseReportSummary(report.Id, report.Title, report.Year, report.Month))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ExpenseReportSummary>(items, request.Page, request.PageSize, totalCount);
    }

    public Task<ExpenseReportDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Set<ExpenseReport>()
            .AsNoTracking()
            .Where(report => report.Id == id)
            .Select(report => new ExpenseReportDetails(report.Id, report.Title, report.UserId, report.Year,
                report.Month))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
