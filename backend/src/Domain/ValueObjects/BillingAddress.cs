using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>Where an expense was billed: an optional store name ("enseigne"), then a French postal address.</summary>
public sealed record BillingAddress
{
    public const int StoreNameMaxLength = 100;

    /// <param name="storeName">Trimmed; null when blank.</param>
    public BillingAddress(string? storeName, Address address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var trimmedStoreName = string.IsNullOrWhiteSpace(storeName) ? null : storeName.Trim();
        if (trimmedStoreName?.Length > StoreNameMaxLength)
        {
            throw new InvalidValueException(nameof(storeName),
                $"The store name must be at most {StoreNameMaxLength} characters.");
        }

        StoreName = trimmedStoreName;
        Address = address;
    }

    /// <summary>For EF Core only, which then sets the properties it loads.</summary>
    private BillingAddress()
    {
        Address = null!;
    }

    public string? StoreName { get; }
    public Address Address { get; }
}
