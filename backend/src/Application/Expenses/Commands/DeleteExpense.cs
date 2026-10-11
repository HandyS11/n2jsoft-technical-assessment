using Application.Exceptions;
using Application.ExpenseReports;
using Application.Persistence;
using Application.Users;

namespace Application.Expenses.Commands;

public sealed record DeleteExpenseCommand(Guid ReportId, Guid Id);

/// <summary>A logical deletion: the expense stays stored but disappears from every query and frees a quota slot.</summary>
public sealed class DeleteExpenseHandler(
    ITransactionRunner transactions,
    IExpenseReportStore expenseReportStore,
    IUserStore userStore,
    IExpenseStore expenseStore,
    TimeProvider timeProvider)
{
    public Task HandleAsync(DeleteExpenseCommand command, CancellationToken cancellationToken)
    {
        return transactions.RunAsync(
            async token =>
            {
                var (report, _) = await ExpenseChanges
                    .LockReportAndUserAsync(expenseReportStore, userStore, command.ReportId, token)
                    .ConfigureAwait(false);
                var expense = await expenseStore.FindAsync(report.Id, command.Id, token).ConfigureAwait(false)
                              ?? throw new NotFoundException("Expense", command.Id);
                expense.Delete(timeProvider.GetUtcNow());
                await expenseStore.SaveChangesAsync(token).ConfigureAwait(false);
            },
            cancellationToken);
    }
}
