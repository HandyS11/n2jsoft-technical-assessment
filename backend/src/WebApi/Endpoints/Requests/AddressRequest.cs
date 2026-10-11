using System.ComponentModel.DataAnnotations;
using Domain.ValueObjects;

namespace WebApi.Endpoints.Requests;

/// <summary>[0-9], not \d: \d also matches non-ASCII digits, which <see cref="Address" /> rejects.</summary>
public sealed record AddressRequest(
    [property: Required]
    [property: StringLength(Address.StreetMaxLength)]
    string Street,
    [property: Required]
    [property: RegularExpression("^[0-9]{5}$")]
    string PostalCode,
    [property: Required]
    [property: StringLength(Address.CityMaxLength)]
    string City)
{
    public Address ToAddress()
    {
        return new Address(Street, PostalCode, City);
    }
}
