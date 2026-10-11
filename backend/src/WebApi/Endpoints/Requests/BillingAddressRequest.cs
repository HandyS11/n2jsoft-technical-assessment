using System.ComponentModel.DataAnnotations;
using Domain.ValueObjects;

namespace WebApi.Endpoints.Requests;

/// <summary>The store name is optional; the rest follows <see cref="AddressRequest" />.</summary>
public sealed record BillingAddressRequest(
    [property: StringLength(BillingAddress.StoreNameMaxLength)]
    string? StoreName,
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
    public BillingAddress ToBillingAddress()
    {
        return new BillingAddress(StoreName, new Address(Street, PostalCode, City));
    }
}
