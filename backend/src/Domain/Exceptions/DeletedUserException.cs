namespace Domain.Exceptions;

/// <summary>The report's user is deleted: the report stays readable, but its expenses can no longer change.</summary>
public sealed class DeletedUserException(Guid userId)
    : BusinessRuleViolationException(
        "expense_report.deleted_user",
        $"User '{userId}' is deleted: the expenses of their reports can no longer change.");
