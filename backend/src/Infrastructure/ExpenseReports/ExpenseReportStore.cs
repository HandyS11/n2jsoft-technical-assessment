using Application.Exceptions;
using Application.ExpenseReports;
using Application.Paging;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

    public Task<bool> ExistsAsync(Guid userId, ReportMonth period, CancellationToken cancellationToken)
    {
        return dbContext.Set<ExpenseReport>().AnyAsync(
            report => report.UserId == userId && report.Year == period.Year && report.Month == period.Month,
            cancellationToken);
    }

    public Task<ExpenseReport?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Set<ExpenseReport>().SingleOrDefaultAsync(report => report.Id == id, cancellationToken);
    }

    public void Add(ExpenseReport report)
    {
        dbContext.Set<ExpenseReport>().Add(report);
    }

    public void Remove(ExpenseReport report)
    {
        dbContext.Set<ExpenseReport>().Remove(report);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // Reports are never updated: a row that was not there to delete was deleted by another request.
            var entry = exception.Entries[0];
            throw new NotFoundException(entry.Metadata.ClrType.Name, entry.Property("Id").CurrentValue!, exception);
        }
        catch (DbUpdateException exception) when (IsDuplicateReport(exception))
        {
            // Another request created the same report between ExistsAsync and this save.
            throw new DuplicateExpenseReportException(exception);
        }
    }

    private static bool IsDuplicateReport(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ExpenseReportEntityTypeConfiguration.UserAndMonthIndexName,
        };
    }
}
