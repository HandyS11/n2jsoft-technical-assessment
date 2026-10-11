namespace Domain.Exceptions;

/// <summary>
/// Base for every business rule broken by a domain operation; mapped to 422 with <see cref="Code" /> as a stable
/// identifier the frontend translates (e.g. "expense.quota_exceeded").
/// </summary>
public abstract class BusinessRuleViolationException(string code, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Code { get; } = code;
}
