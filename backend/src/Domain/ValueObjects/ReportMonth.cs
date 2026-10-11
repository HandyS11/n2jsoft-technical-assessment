using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>The calendar month an expense report covers, from January 2000 to December 2100.</summary>
public sealed record ReportMonth
{
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    /// <summary>
    /// Hard-coded rather than read from CultureInfo("fr-FR"): a title must depend neither on ICU being installed nor on
    /// a culture's capitalisation.
    /// </summary>
    private static readonly string[] FrenchNames =
    [
        "Janvier", "Février", "Mars", "Avril", "Mai", "Juin",
        "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre",
    ];

    public ReportMonth(int year, int month)
    {
        if (year is < MinYear or > MaxYear)
        {
            throw new InvalidValueException(nameof(year), $"The year must be between {MinYear} and {MaxYear}.");
        }

        if (month is < 1 or > 12)
        {
            throw new InvalidValueException(nameof(month), "The month must be between 1 and 12.");
        }

        Year = year;
        Month = month;
    }

    public int Year { get; }

    /// <summary>1 for January to 12 for December.</summary>
    public int Month { get; }

    /// <summary>The French label used in report titles: "Octobre 2025".</summary>
    public override string ToString()
    {
        return $"{FrenchNames[Month - 1]} {Year}";
    }
}
