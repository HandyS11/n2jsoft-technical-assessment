namespace Domain.Exceptions;

/// <summary>The user already has as many expenses this month as their monthly quota allows.</summary>
public sealed class ExpenseQuotaExceededException(Guid userId, int monthlyExpenseQuota)
    : BusinessRuleViolationException(
        "expense.quota_exceeded",
        $"User '{userId}' has reached their monthly quota of {monthlyExpenseQuota} expenses.");
