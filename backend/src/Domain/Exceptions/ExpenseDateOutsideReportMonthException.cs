using Domain.ValueObjects;

namespace Domain.Exceptions;

public sealed class ExpenseDateOutsideReportMonthException(DateOnly date, ReportMonth period)
    : BusinessRuleViolationException(
        "expense.date_outside_report_month",
        $"The date {date:yyyy-MM-dd} is outside the report's month ({period.Year}-{period.Month:00}).");
