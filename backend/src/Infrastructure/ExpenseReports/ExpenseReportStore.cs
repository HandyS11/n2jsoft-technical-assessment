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
        var items = await reports
            .OrderByDescending(report => report.Year)
            .ThenByDescending(report => report.Month)
            .ThenBy(report => report.Title)
            // Reports sharing a title (two users with the same name) keep the same order from one page to the next.
            .ThenBy(report => report.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(report => new ExpenseReportSummary(
                report.Id,
                report.Title,
                report.Year,
                report.Month,
                // The expenses' query filter applies here too: deleted expenses are left out.
                dbContext.Set<Expense>().Count(expense => expense.ExpenseReportId == report.Id),
                dbContext.Set<Expense>()
                    .Where(expense => expense.ExpenseReportId == report.Id)
                    .Sum(expense => expense.Amount)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ExpenseReportSummary>(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<ExpenseReportDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var report = await dbContext.Set<ExpenseReport>()
            .AsNoTracking()
            .Where(report => report.Id == id)
            .Select(report => new
            {
                report.Id,
                report.Title,
                report.UserId,
                report.Year,
                report.Month,
                ExpenseCount = dbContext.Set<Expense>().Count(expense => expense.ExpenseReportId == report.Id),
                Total = dbContext.Set<Expense>()
                    .Where(expense => expense.ExpenseReportId == report.Id)
                    .Sum(expense => expense.Amount),
            })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (report is null)
        {
            return null;
        }

        // A query of its own: IgnoreQueryFilters would also bring the deleted expenses back into the counts above.
        var user = await dbContext.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.Id == report.UserId)
            .Select(user => new
            {
                user.MonthlyExpenseQuota, IsDeleted = user.DeletedAt != null
            })
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ExpenseReportDetails(report.Id, report.Title, report.UserId, report.Year, report.Month,
            report.ExpenseCount, report.Total, user.MonthlyExpenseQuota, user.IsDeleted);
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
}
