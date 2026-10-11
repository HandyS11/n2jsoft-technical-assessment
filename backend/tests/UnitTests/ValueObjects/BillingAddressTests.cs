using Domain.Exceptions;
using Domain.ValueObjects;

namespace UnitTests.ValueObjects;

public sealed class BillingAddressTests
{
    private static readonly Address Paris = new("12 rue de Rivoli", "75001", "Paris");

    [Fact]
    public void Keeps_the_store_name_trimmed_and_the_address()
    {
        var billingAddress = new BillingAddress("  Fnac ", Paris);

        billingAddress.StoreName.ShouldBe("Fnac");
        billingAddress.Address.ShouldBe(Paris);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Has_no_store_name_when_none_is_given(string? storeName)
    {
        new BillingAddress(storeName, Paris).StoreName.ShouldBeNull();
    }

    [Fact]
    public void Accepts_a_store_name_of_100_characters()
    {
        var storeName = new string('a', BillingAddress.StoreNameMaxLength);

        new BillingAddress(storeName, Paris).StoreName.ShouldBe(storeName);
    }

    [Fact]
    public void Rejects_a_store_name_longer_than_100_characters()
    {
        var storeName = new string('a', BillingAddress.StoreNameMaxLength + 1);

        Should.Throw<InvalidValueException>(() => new BillingAddress(storeName, Paris)).ParameterName
            .ShouldBe("storeName");
    }

    [Fact]
    public void Requires_an_address()
    {
        Should.Throw<ArgumentNullException>(() => new BillingAddress("Fnac", null!));
    }

    [Fact]
    public void Two_billing_addresses_with_the_same_parts_are_equal()
    {
        new BillingAddress("Fnac", Paris).ShouldBe(new BillingAddress("Fnac", new Address("12 rue de Rivoli", "75001",
            "Paris")));
    }
}
