using Application.Exceptions;
using Application.Persistence;
using Application.Users;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace Application.ExpenseReports.Commands;

public sealed record CreateExpenseReportCommand(Guid UserId, ReportMonth Period);

public sealed class CreateExpenseReportHandler(
    ITransactionRunner transactions,
    IUserStore userStore,
    IExpenseReportStore expenseReportStore)
{
    public Task<ExpenseReportDetails> HandleAsync(
        CreateExpenseReportCommand command,
        CancellationToken cancellationToken)
    {
        return transactions.RunAsync(
            async token =>
            {
                // Locked until the report is saved: a deactivation or deletion of the user either waits for it, or
                // was saved first and is seen here.
                var user = await userStore.FindLockedAsync(command.UserId, token).ConfigureAwait(false)
                           ?? throw new NotFoundException("User", command.UserId);
                if (await expenseReportStore.ExistsAsync(user.Id, command.Period, token).ConfigureAwait(false))
                {
                    throw new DuplicateExpenseReportException();
                }

                var report = ExpenseReport.Create(user, command.Period);
                expenseReportStore.Add(report);
                await expenseReportStore.SaveChangesAsync(token).ConfigureAwait(false);
                return ExpenseReportDetails.From(report);
            },
            cancellationToken);
    }
}
