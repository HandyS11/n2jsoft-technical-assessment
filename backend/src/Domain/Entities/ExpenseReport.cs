using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities;

/// <summary>A user's expenses for one calendar month. Nothing changes after creation; a deletion is physical.</summary>
public sealed class ExpenseReport
{
    /// <summary>Room for two names of <see cref="User.NameMaxLength" />, the separators and any month label.</summary>
    public const int TitleMaxLength = 250;

    /// <summary>Only <see cref="Create" /> and EF Core, which then sets the properties it loads, build reports.</summary>
    private ExpenseReport()
    {
    }

    public Guid Id { get; } = Guid.CreateVersion7();
    public Guid UserId { get; private set; }

    /// <summary>
    /// <see cref="Year" /> and <see cref="Month" /> are stored as two columns rather than as a complex property: EF Core
    /// cannot index a complex property's columns, and (user, year, month) is unique.
    /// </summary>
    public int Year { get; private set; }

    public int Month { get; private set; }

    public ReportMonth Period => new(Year, Month);

    /// <summary>"First Last - Month Year", fixed at creation: renaming or deleting the user leaves it unchanged.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Throws <see cref="InactiveUserException" /> (422) when the user is inactive.</summary>
    public static ExpenseReport Create(User user, ReportMonth period)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(period);
        if (!user.IsActive)
        {
            throw new InactiveUserException(user.Id);
        }

        return new ExpenseReport
        {
            UserId = user.Id,
            Year = period.Year,
            Month = period.Month,
            Title = $"{user.FirstName} {user.LastName} - {period}",
        };
    }
}
