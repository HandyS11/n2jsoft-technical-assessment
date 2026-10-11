namespace Application.Exceptions;

/// <summary>The resource changed between the moment it was read and the moment the change was saved.</summary>
public sealed class ConcurrencyConflictException(string resource, object key, Exception? innerException = null)
    : Exception($"{resource} '{key}' was changed by another request.", innerException);
