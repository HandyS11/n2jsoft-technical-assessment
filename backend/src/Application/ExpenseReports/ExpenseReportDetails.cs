using Domain.Entities;

namespace Application.ExpenseReports;

public sealed record ExpenseReportDetails(Guid Id, string Title, Guid UserId, int Year, int Month)
{
    public static ExpenseReportDetails From(ExpenseReport report)
    {
        return new ExpenseReportDetails(report.Id, report.Title, report.UserId, report.Year, report.Month);
    }
}
