using Application.ExpenseReports;
using Application.Persistence;
using Application.Users;
using Domain.Entities;
using Domain.ValueObjects;

namespace Application.Expenses.Commands;

public sealed record CreateExpenseCommand(
    Guid ReportId,
    DateOnly Date,
    string Description,
    decimal Amount,
    BillingAddress BillingAddress);

public sealed class CreateExpenseHandler(
    ITransactionRunner transactions,
    IExpenseReportStore expenseReportStore,
    IUserStore userStore,
    IExpenseStore expenseStore)
{
    public Task<ExpenseDetails> HandleAsync(CreateExpenseCommand command, CancellationToken cancellationToken)
    {
        return transactions.RunAsync(
            async token =>
            {
                var (report, user) = await ExpenseChanges
                    .LockReportAndUserAsync(expenseReportStore, userStore, command.ReportId, token)
                    .ConfigureAwait(false);
                var liveExpenseCount = await expenseStore.CountLiveAsync(report.Id, token).ConfigureAwait(false);
                var expense = Expense.Create(report, user, liveExpenseCount, command.Date, command.Description,
                    command.Amount, command.BillingAddress);
                expenseStore.Add(expense);
                await expenseStore.SaveChangesAsync(token).ConfigureAwait(false);
                return ExpenseDetails.From(expense);
            },
            cancellationToken);
    }
}
