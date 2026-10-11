using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities;

public sealed class User
{
    public const int NameMaxLength = 100;

    /// <summary>Only <see cref="Create" /> and EF Core, which then sets the properties it loads, build users.</summary>
    private User()
    {
    }

    public Guid Id { get; } = Guid.CreateVersion7();
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public Address Address { get; private set; } = null!;

    /// <summary>The most expenses the user may have in one calendar month (enforced when an expense is created).</summary>
    public int MonthlyExpenseQuota { get; private set; }

    /// <summary>Only active users can be assigned to an expense report.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Set by a logical deletion; a deleted user is hidden from every query.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    public static User Create(string firstName, string lastName, Address address, int monthlyExpenseQuota)
    {
        var user = new User
        {
            IsActive = true
        };
        user.UpdateDetails(firstName, lastName, address, monthlyExpenseQuota);
        return user;
    }

    /// <summary>Checks every detail before changing any, so a rejected update leaves the user as it was.</summary>
    public void UpdateDetails(string firstName, string lastName, Address address, int monthlyExpenseQuota)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (monthlyExpenseQuota < 1)
        {
            throw new InvalidValueException(nameof(monthlyExpenseQuota),
                "The monthly expense quota must be at least 1.");
        }

        var trimmedFirstName = Text.Required(firstName, NameMaxLength);
        var trimmedLastName = Text.Required(lastName, NameMaxLength);

        FirstName = trimmedFirstName;
        LastName = trimmedLastName;
        Address = address;
        MonthlyExpenseQuota = monthlyExpenseQuota;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Delete(DateTimeOffset now)
    {
        DeletedAt ??= now;
    }
}
