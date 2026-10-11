using System.Runtime.CompilerServices;
using Domain.Exceptions;

namespace Domain;

internal static class Text
{
    /// <summary>Trims <paramref name="value" /> and checks it is neither blank nor longer than <paramref name="maxLength" />.</summary>
    public static string Required(
        string value,
        int maxLength,
        [CallerArgumentExpression(nameof(value))]
        string parameterName = "")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidValueException(parameterName, "The value must not be blank.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new InvalidValueException(parameterName, $"The value must be at most {maxLength} characters.");
        }

        return trimmed;
    }
}
