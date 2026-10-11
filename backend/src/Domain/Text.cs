using System.Runtime.CompilerServices;

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
            throw new ArgumentException("The value must not be blank.", parameterName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"The value must be at most {maxLength} characters.", parameterName);
        }

        return trimmed;
    }
}
