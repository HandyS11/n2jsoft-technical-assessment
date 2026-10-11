using Domain.Exceptions;
using Domain.ValueObjects;

namespace UnitTests.ValueObjects;

public sealed class AddressTests
{
    [Fact]
    public void Trims_every_part()
    {
        var address = new Address("  12 rue de Rivoli ", " 75001 ", " Paris  ");

        address.Street.ShouldBe("12 rue de Rivoli");
        address.PostalCode.ShouldBe("75001");
        address.City.ShouldBe("Paris");
    }

    [Fact]
    public void Two_addresses_with_the_same_parts_are_equal()
    {
        new Address("12 rue de Rivoli", "75001", "Paris").ShouldBe(new Address("12 rue de Rivoli", "75001", "Paris"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_blank_street(string street)
    {
        Should.Throw<InvalidValueException>(() => new Address(street, "75001", "Paris")).ParameterName
            .ShouldBe("street");
    }

    [Fact]
    public void Rejects_a_street_longer_than_200_characters()
    {
        var street = new string('a', Address.StreetMaxLength + 1);

        Should.Throw<InvalidValueException>(() => new Address(street, "75001", "Paris")).ParameterName
            .ShouldBe("street");
    }

    [Fact]
    public void Accepts_a_street_of_200_characters()
    {
        var street = new string('a', Address.StreetMaxLength);

        new Address(street, "75001", "Paris").Street.ShouldBe(street);
    }

    [Theory]
    [InlineData("")]
    [InlineData("7500")]
    [InlineData("750011")]
    [InlineData("75O01")]
    [InlineData("٧٥٠٠١")]
    public void Rejects_a_postal_code_that_is_not_five_digits(string postalCode)
    {
        Should.Throw<InvalidValueException>(() => new Address("12 rue de Rivoli", postalCode, "Paris"))
            .ParameterName.ShouldBe("postalCode");
    }

    [Fact]
    public void Rejects_a_missing_postal_code()
    {
        Should.Throw<InvalidValueException>(() => new Address("12 rue de Rivoli", null!, "Paris"))
            .ParameterName.ShouldBe("postalCode");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_blank_city(string city)
    {
        Should.Throw<InvalidValueException>(() => new Address("12 rue de Rivoli", "75001", city)).ParameterName
            .ShouldBe("city");
    }

    [Fact]
    public void Rejects_a_city_longer_than_100_characters()
    {
        var city = new string('a', Address.CityMaxLength + 1);

        Should.Throw<InvalidValueException>(() => new Address("12 rue de Rivoli", "75001", city)).ParameterName
            .ShouldBe("city");
    }
}
