using System.Globalization;

namespace AcceptanceTests.Support;

/// <summary>Amounts are written "23.50" in the feature files: a point, always two decimals.</summary>
public static class Amounts
{
    public static decimal Parse(string value)
    {
        return decimal.Parse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture);
    }

    public static string Format(decimal amount)
    {
        return amount.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
