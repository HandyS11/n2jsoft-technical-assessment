namespace Domain.Exceptions;

/// <summary>A user has at most one expense report per month.</summary>
public sealed class DuplicateExpenseReportException(Exception? innerException = null)
    : BusinessRuleViolationException(
        "expense_report.already_exists",
        "The user already has an expense report for this month.",
        innerException);
