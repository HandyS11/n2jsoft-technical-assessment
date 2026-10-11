using Application.Exceptions;
using Application.ExpenseReports;
using Application.Persistence;
using Application.Users;
using Domain.ValueObjects;

namespace Application.Expenses.Commands;

/// <param name="ExpectedVersion">
/// The version the client read; when given, the update is rejected if the expense changed since. Null overwrites
/// whatever is stored.
/// </param>
public sealed record UpdateExpenseCommand(
    Guid ReportId,
    Guid Id,
    DateOnly Date,
    string Description,
    decimal Amount,
    BillingAddress BillingAddress,
    uint? ExpectedVersion = null);

/// <summary>Never checks the quota: the date stays within the report's month, so the month's count cannot change.</summary>
public sealed class UpdateExpenseHandler(
    ITransactionRunner transactions,
    IExpenseReportStore expenseReportStore,
    IUserStore userStore,
    IExpenseStore expenseStore)
{
    public Task HandleAsync(UpdateExpenseCommand command, CancellationToken cancellationToken)
    {
        return transactions.RunAsync(
            async token =>
            {
                var (report, _) = await ExpenseChanges
                    .LockReportAndUserAsync(expenseReportStore, userStore, command.ReportId, token)
                    .ConfigureAwait(false);
                var expense = await expenseStore.FindAsync(report.Id, command.Id, token).ConfigureAwait(false)
                              ?? throw new NotFoundException("Expense", command.Id);
                if (command.ExpectedVersion is { } expectedVersion && expectedVersion != expense.Version)
                {
                    throw new ConcurrencyConflictException("Expense", command.Id);
                }

                expense.Update(report, command.Date, command.Description, command.Amount, command.BillingAddress);
                await expenseStore.SaveChangesAsync(token).ConfigureAwait(false);
            },
            cancellationToken);
    }
}
