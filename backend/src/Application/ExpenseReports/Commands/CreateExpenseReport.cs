using Application.Users;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace Application.ExpenseReports.Commands;

public sealed record CreateExpenseReportCommand(Guid UserId, ReportMonth Period);

public sealed class CreateExpenseReportHandler(IUserStore userStore, IExpenseReportStore expenseReportStore)
{
    public async Task<ExpenseReportDetails> HandleAsync(
        CreateExpenseReportCommand command,
        CancellationToken cancellationToken)
    {
        var user = await userStore.GetAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        if (await expenseReportStore.ExistsAsync(user.Id, command.Period, cancellationToken).ConfigureAwait(false))
        {
            throw new DuplicateExpenseReportException();
        }

        var report = ExpenseReport.Create(user, command.Period);
        expenseReportStore.Add(report);
        await expenseReportStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ExpenseReportDetails.From(report);
    }
}
