using Application.Expenses;
using Application.Paging;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Expenses;

internal sealed class ExpenseStore(AppDbContext dbContext) : IExpenseStore
{
    public async Task<PagedResult<ExpenseSummary>> ListAsync(
        Guid reportId,
        PageRequest request,
        CancellationToken cancellationToken)
    {
        var expenses = dbContext.Set<Expense>().AsNoTracking().Where(expense => expense.ExpenseReportId == reportId);
        var totalCount = await expenses.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await expenses
            .OrderBy(expense => expense.Date)
            // Version 7 ids grow with time: expenses of the same day keep their creation order.
            .ThenBy(expense => expense.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(expense => new ExpenseSummary(
                expense.Id,
                expense.Date,
                expense.Description,
                expense.Amount,
                ExpenseBillingAddress.From(expense.BillingAddress)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ExpenseSummary>(items, request.Page, request.PageSize, totalCount);
    }

    public Task<ExpenseDetails?> GetDetailsAsync(Guid reportId, Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Set<Expense>()
            .AsNoTracking()
            .Where(expense => expense.ExpenseReportId == reportId && expense.Id == id)
            .Select(expense => new ExpenseDetails(
                expense.Id,
                expense.Date,
                expense.Description,
                expense.Amount,
                ExpenseBillingAddress.From(expense.BillingAddress),
                expense.Version))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountLiveAsync(Guid reportId, CancellationToken cancellationToken)
    {
        return dbContext.Set<Expense>().CountAsync(expense => expense.ExpenseReportId == reportId, cancellationToken);
    }

    public Task<Expense?> FindAsync(Guid reportId, Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Set<Expense>()
            .SingleOrDefaultAsync(expense => expense.ExpenseReportId == reportId && expense.Id == id,
                cancellationToken);
    }

    public void Add(Expense expense)
    {
        dbContext.Set<Expense>().Add(expense);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
