using Domain.Entities;

namespace Application.ExpenseReports;

/// <param name="ExpenseCount">The report's expenses, deleted ones left out: what the monthly quota counts.</param>
/// <param name="Total">The sum of their amounts, in euros.</param>
/// <param name="MonthlyExpenseQuota">The user's current quota.</param>
/// <param name="IsReadOnly">The user is deleted: the report's expenses can no longer change.</param>
public sealed record ExpenseReportDetails(
    Guid Id,
    string Title,
    Guid UserId,
    int Year,
    int Month,
    int ExpenseCount,
    decimal Total,
    int MonthlyExpenseQuota,
    bool IsReadOnly)
{
    /// <summary>A report just created for <paramref name="user" />, who is active: no expense yet.</summary>
    public static ExpenseReportDetails From(ExpenseReport report, User user)
    {
        return new ExpenseReportDetails(report.Id, report.Title, report.UserId, report.Year, report.Month, 0, 0m,
            user.MonthlyExpenseQuota, false);
    }
}
