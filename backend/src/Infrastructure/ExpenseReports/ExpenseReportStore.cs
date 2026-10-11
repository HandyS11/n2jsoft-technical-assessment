using Application.ExpenseReports;
using Application.Paging;
using Domain.Entities;
using Domain.ValueObjects;
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
        var page = reports
            .OrderByDescending(report => report.Year)
            .ThenByDescending(report => report.Month)
            .ThenBy(report => report.Title)
            // Reports sharing a title (two users with the same name) keep the same order from one page to the next.
            .ThenBy(report => report.Id)
            .Skip(request.Skip)
            .Take(request.PageSize);
        var items = await WithExpenses(page)
            .Select(row => new ExpenseReportSummary(
                row.Report.Id,
                row.Report.Title,
                row.Report.Year,
                row.Report.Month,
                row.ExpenseCount,
                row.Total))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ExpenseReportSummary>(items, request.Page, request.PageSize, totalCount);
    }

    public Task<ExpenseReportDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var reports = dbContext.Set<ExpenseReport>()
            .AsNoTracking()
            // A deleted user's report is still shown, read-only. Only the users' filter is lifted: deleted expenses
            // stay out of the counts.
            .IgnoreQueryFilters([QueryFilters.NotDeletedUsers])
            .Where(report => report.Id == id);
        return (
                from row in WithExpenses(reports)
                join user in dbContext.Set<User>() on row.Report.UserId equals user.Id
                select new ExpenseReportDetails(row.Report.Id, row.Report.Title, row.Report.UserId, row.Report.Year,
                    row.Report.Month, row.ExpenseCount, row.Total, user.MonthlyExpenseQuota, user.DeletedAt != null))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid userId, ReportMonth period, CancellationToken cancellationToken)
    {
        return dbContext.Set<ExpenseReport>().AnyAsync(
            report => report.UserId == userId && report.Year == period.Year && report.Month == period.Month,
            cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Set<ExpenseReport>().AnyAsync(report => report.Id == id, cancellationToken);
    }

    public Task<ExpenseReport?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Set<ExpenseReport>().SingleOrDefaultAsync(report => report.Id == id, cancellationToken);
    }

    public Task<ExpenseReport?> FindLockedAsync(Guid id, CancellationToken cancellationToken)
    {
        // FOR UPDATE: another request locking the report, or deleting it, waits for the commit.
        return dbContext.Set<ExpenseReport>()
            .FromSql($"""SELECT * FROM expense_reports WHERE "Id" = {id} FOR UPDATE""")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public void Add(ExpenseReport report)
    {
        dbContext.Set<ExpenseReport>().Add(report);
    }

    public void Remove(ExpenseReport report)
    {
        dbContext.Set<ExpenseReport>().Remove(report);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Each report with the count and the total of its expenses, both read in one pass over them. The expenses' query
    /// filter applies: deleted ones are left out.
    /// </summary>
    private IQueryable<ReportWithExpenses> WithExpenses(IQueryable<ExpenseReport> reports)
    {
        return from report in reports
            from expenses in dbContext.Set<Expense>()
                .Where(expense => expense.ExpenseReportId == report.Id)
                .GroupBy(expense => expense.ExpenseReportId)
                .Select(sameReport => new
                {
                    Count = (int?)sameReport.Count(), Total = (decimal?)sameReport.Sum(expense => expense.Amount)
                })
                // A report without expenses has no group: its count and total are 0.
                .DefaultIfEmpty()
            select new ReportWithExpenses
            {
                Report = report, ExpenseCount = expenses.Count ?? 0, Total = expenses.Total ?? 0m
            };
    }

    /// <summary>Set by member initialization, not by a constructor, so that EF Core can query further through it.</summary>
    private sealed class ReportWithExpenses
    {
        public required ExpenseReport Report { get; init; }
        public required int ExpenseCount { get; init; }
        public required decimal Total { get; init; }
    }
}
