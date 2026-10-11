namespace Domain.Exceptions;

/// <summary>
/// A value a domain object rejects, such as a blank name; mapped to 400 like the request validation errors, keyed by
/// <see cref="ParameterName" />.
/// </summary>
public sealed class InvalidValueException(string parameterName, string message) : Exception(message)
{
    public string ParameterName { get; } = parameterName;
}
