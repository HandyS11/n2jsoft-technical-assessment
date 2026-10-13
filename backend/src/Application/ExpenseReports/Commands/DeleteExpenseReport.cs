using Application.Exceptions;

namespace Application.ExpenseReports.Commands;

public sealed record DeleteExpenseReportCommand(Guid Id);

/// <summary>A physical deletion: the report is gone for good, and the database cascades it to its expenses.</summary>
public sealed class DeleteExpenseReportHandler(IExpenseReportStore expenseReportStore)
{
    public async Task HandleAsync(DeleteExpenseReportCommand command, CancellationToken cancellationToken)
    {
        var report = await expenseReportStore.FindAsync(command.Id, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("ExpenseReport", command.Id);
        expenseReportStore.Remove(report);
        await expenseReportStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
