using System.Globalization;

namespace AcceptanceTests.Support;

/// <summary>Dates are written "2025-10-15" in the feature files.</summary>
public static class Dates
{
    private const string Pattern = "yyyy-MM-dd";

    public static DateOnly Parse(string value)
    {
        return DateOnly.ParseExact(value, Pattern, CultureInfo.InvariantCulture);
    }

    public static string Format(DateOnly date)
    {
        return date.ToString(Pattern, CultureInfo.InvariantCulture);
    }
}
