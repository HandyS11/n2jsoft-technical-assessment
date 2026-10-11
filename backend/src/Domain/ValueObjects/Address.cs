using Domain.Exceptions;

namespace Domain.ValueObjects;

/// <summary>A French postal address: the street (with its number), a five-digit postal code and the city.</summary>
public sealed record Address
{
    public const int StreetMaxLength = 200;
    public const int CityMaxLength = 100;
    public const int PostalCodeLength = 5;

    public Address(string street, string postalCode, string city)
    {
        Street = Text.Required(street, StreetMaxLength);
        PostalCode = postalCode.Trim();
        if (PostalCode.Length != PostalCodeLength || !PostalCode.All(char.IsAsciiDigit))
        {
            throw new InvalidValueException(nameof(postalCode), $"The postal code must be {PostalCodeLength} digits.");
        }

        City = Text.Required(city, CityMaxLength);
    }

    public string Street { get; }
    public string PostalCode { get; }
    public string City { get; }
}
