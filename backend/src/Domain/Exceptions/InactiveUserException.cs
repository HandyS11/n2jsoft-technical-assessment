namespace Domain.Exceptions;

public sealed class InactiveUserException(Guid userId)
    : BusinessRuleViolationException(
        "expense_report.inactive_user",
        $"User '{userId}' is inactive: only active users can be assigned to an expense report.");
