using Domain.Exceptions;
using Domain.ValueObjects;

namespace UnitTests.ValueObjects;

public sealed class ReportMonthTests
{
    [Fact]
    public void Keeps_the_year_and_the_month()
    {
        var period = new ReportMonth(2025, 10);

        period.Year.ShouldBe(2025);
        period.Month.ShouldBe(10);
    }

    [Fact]
    public void Two_months_with_the_same_year_and_month_are_equal()
    {
        new ReportMonth(2025, 10).ShouldBe(new ReportMonth(2025, 10));
    }

    [Theory]
    [InlineData(2000, 1)]
    [InlineData(2100, 12)]
    public void Accepts_the_first_and_the_last_month_allowed(int year, int month)
    {
        Should.NotThrow(() => new ReportMonth(year, month));
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public void Rejects_a_year_out_of_range(int year)
    {
        Should.Throw<InvalidValueException>(() => new ReportMonth(year, 1)).ParameterName.ShouldBe("year");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Rejects_a_month_out_of_range(int month)
    {
        Should.Throw<InvalidValueException>(() => new ReportMonth(2025, month)).ParameterName.ShouldBe("month");
    }

    [Theory]
    [InlineData(1, "Janvier 2025")]
    [InlineData(2, "Février 2025")]
    [InlineData(3, "Mars 2025")]
    [InlineData(4, "Avril 2025")]
    [InlineData(5, "Mai 2025")]
    [InlineData(6, "Juin 2025")]
    [InlineData(7, "Juillet 2025")]
    [InlineData(8, "Août 2025")]
    [InlineData(9, "Septembre 2025")]
    [InlineData(10, "Octobre 2025")]
    [InlineData(11, "Novembre 2025")]
    [InlineData(12, "Décembre 2025")]
    public void Reads_as_the_capitalised_french_month_then_the_year(int month, string expected)
    {
        new ReportMonth(2025, month).ToString().ShouldBe(expected);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(31)]
    public void Contains_every_day_of_its_month(int day)
    {
        new ReportMonth(2025, 10).Contains(new DateOnly(2025, 10, day)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(2025, 9, 30)]
    [InlineData(2025, 11, 1)]
    [InlineData(2024, 10, 15)]
    public void Does_not_contain_a_day_of_another_month(int year, int month, int day)
    {
        new ReportMonth(2025, 10).Contains(new DateOnly(year, month, day)).ShouldBeFalse();
    }
}
