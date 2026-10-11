using Domain.ValueObjects;

namespace Application.Expenses;

public sealed record ExpenseBillingAddress(string? StoreName, string Street, string PostalCode, string City)
{
    public static ExpenseBillingAddress From(BillingAddress billingAddress)
    {
        return new ExpenseBillingAddress(
            billingAddress.StoreName,
            billingAddress.Address.Street,
            billingAddress.Address.PostalCode,
            billingAddress.Address.City);
    }
}
