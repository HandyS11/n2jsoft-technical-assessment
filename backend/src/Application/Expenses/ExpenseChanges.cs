using Application.Exceptions;
using Application.ExpenseReports;
using Application.Users;
using Domain.Entities;
using Domain.Exceptions;

namespace Application.Expenses;

internal static class ExpenseChanges
{
    /// <summary>
    /// The first reads of every expense change, inside its transaction and always in this order (report, then user),
    /// so two changes cannot deadlock. The report stays locked until the commit: changes to its expenses run one after
    /// the other, and a quota counted here stays true until the save. A deletion or quota change of the user waits too.
    /// </summary>
    /// <exception cref="NotFoundException">The report is unknown.</exception>
    /// <exception cref="DeletedUserException">The report's user is deleted: its expenses can no longer change.</exception>
    public static async Task<(ExpenseReport Report, User User)> LockReportAndUserAsync(
        IExpenseReportStore expenseReportStore,
        IUserStore userStore,
        Guid reportId,
        CancellationToken cancellationToken)
    {
        var report = await expenseReportStore.FindLockedAsync(reportId, cancellationToken).ConfigureAwait(false)
                     ?? throw new NotFoundException("ExpenseReport", reportId);
        // Null only for a deleted user: a report's user cannot be unknown (foreign key, users never removed).
        var user = await userStore.FindLockedAsync(report.UserId, cancellationToken).ConfigureAwait(false)
                   ?? throw new DeletedUserException(report.UserId);
        return (report, user);
    }
}
