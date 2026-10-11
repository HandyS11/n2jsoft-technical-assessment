using System.Globalization;
using Domain.ValueObjects;

namespace AcceptanceTests.Support;

/// <summary>Months are written "2025-10" in the feature files.</summary>
public static class Months
{
    private const string Pattern = "yyyy-MM";

    public static ReportMonth Parse(string value)
    {
        var date = DateOnly.ParseExact(value, Pattern, CultureInfo.InvariantCulture);
        return new ReportMonth(date.Year, date.Month);
    }

    public static string Format(int year, int month)
    {
        return new DateOnly(year, month, 1).ToString(Pattern, CultureInfo.InvariantCulture);
    }
}
