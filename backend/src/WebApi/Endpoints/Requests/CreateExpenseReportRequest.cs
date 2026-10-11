using System.ComponentModel.DataAnnotations;
using Application.ExpenseReports.Commands;
using Domain.ValueObjects;

namespace WebApi.Endpoints.Requests;

/// <summary>Nullable, so that a missing field answers 400 instead of binding to Guid.Empty or 0.</summary>
public sealed record CreateExpenseReportRequest(
    [property: Required] Guid? UserId,
    [property: Required]
    [property: Range(ReportMonth.MinYear, ReportMonth.MaxYear)]
    int? Year,
    [property: Required]
    [property: Range(1, 12)]
    int? Month)
{
    /// <summary>Called once validation has run, so every value is present.</summary>
    public CreateExpenseReportCommand ToCommand()
    {
        return new CreateExpenseReportCommand(UserId!.Value, new ReportMonth(Year!.Value, Month!.Value));
    }
}
