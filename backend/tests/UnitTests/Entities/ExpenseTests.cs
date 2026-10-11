using Domain.Entities;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace UnitTests.Entities;

public sealed class ExpenseTests
{
    private static readonly Address Paris = new("12 rue de Rivoli", "75001", "Paris");
    private static readonly BillingAddress Fnac = new("Fnac", Paris);
    private static readonly DateOnly October15 = new(2025, 10, 15);

    private readonly ExpenseReport _report;
    private readonly User _user = User.Create("Juste", "Leblanc", Paris, 3);

    public ExpenseTests()
    {
        _report = ExpenseReport.Create(_user, new ReportMonth(2025, 10));
    }

    [Fact]
    public void Create_records_every_detail_in_the_report()
    {
        var expense = Create(description: "  Taxi  ", amount: 23.5m);

        expense.Id.ShouldNotBe(Guid.Empty);
        expense.ExpenseReportId.ShouldBe(_report.Id);
        expense.Date.ShouldBe(October15);
        expense.Description.ShouldBe("Taxi");
        expense.Amount.ShouldBe(23.5m);
        expense.BillingAddress.ShouldBe(Fnac);
        expense.DeletedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    public void Create_accepts_the_first_and_last_day_of_the_report_month(int day)
    {
        Create(new DateOnly(2025, 10, day)).Date.Day.ShouldBe(day);
    }

    [Theory]
    [InlineData(2025, 9, 30)]
    [InlineData(2025, 11, 1)]
    public void Create_refuses_a_date_outside_the_report_month(int year, int month, int day)
    {
        Should.Throw<ExpenseDateOutsideReportMonthException>(() => Create(new DateOnly(year, month, day)))
            .Code.ShouldBe("expense.date_outside_report_month");
    }

    [Fact]
    public void Create_accepts_the_last_expense_the_quota_allows()
    {
        Should.NotThrow(() => Create(liveExpenseCount: _user.MonthlyExpenseQuota - 1));
    }

    [Fact]
    public void Create_refuses_an_expense_once_the_quota_is_reached()
    {
        Should.Throw<ExpenseQuotaExceededException>(() => Create(liveExpenseCount: _user.MonthlyExpenseQuota))
            .Code.ShouldBe("expense.quota_exceeded");
    }

    [Fact]
    public void Create_refuses_a_user_who_is_not_the_report_s()
    {
        var other = User.Create("Marc", "Assin", Paris, 10);

        Should.Throw<ArgumentException>(() =>
            Expense.Create(_report, other, 0, October15, "Taxi", 10m, Fnac));
    }

    [Fact]
    public void Create_accepts_a_description_of_50_characters()
    {
        var description = new string('a', Expense.DescriptionMaxLength);

        Create(description: description).Description.ShouldBe(description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void Create_rejects_a_blank_or_too_long_description(string description)
    {
        Should.Throw<InvalidValueException>(() => Create(description: description)).ParameterName
            .ShouldBe("description");
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("999999.99")]
    public void Create_accepts_the_smallest_and_largest_amounts(string amount)
    {
        var value = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);

        Create(amount: value).Amount.ShouldBe(value);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("12.345")]
    [InlineData("1000000")]
    public void Create_rejects_an_amount_out_of_range_or_with_more_than_two_decimals(string amount)
    {
        var value = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture);

        Should.Throw<InvalidValueException>(() => Create(amount: value)).ParameterName.ShouldBe("amount");
    }

    [Fact]
    public void Create_checks_the_details_before_the_quota()
    {
        Should.Throw<InvalidValueException>(() =>
            Create(description: "", liveExpenseCount: _user.MonthlyExpenseQuota));
    }

    [Fact]
    public void Update_replaces_every_detail()
    {
        var expense = Create();
        var lyon = new BillingAddress(null, new Address("3 place Bellecour", "69002", "Lyon"));

        expense.Update(_report, new DateOnly(2025, 10, 2), "Hôtel", 120m, lyon);

        expense.Date.ShouldBe(new DateOnly(2025, 10, 2));
        expense.Description.ShouldBe("Hôtel");
        expense.Amount.ShouldBe(120m);
        expense.BillingAddress.ShouldBe(lyon);
    }

    [Fact]
    public void Update_refuses_a_date_outside_the_report_month_and_leaves_the_expense_unchanged()
    {
        var expense = Create();

        Should.Throw<ExpenseDateOutsideReportMonthException>(() =>
            expense.Update(_report, new DateOnly(2025, 11, 1), "Hôtel", 120m, Fnac));

        expense.Date.ShouldBe(October15);
        expense.Description.ShouldBe("Taxi");
    }

    [Fact]
    public void Update_refuses_an_invalid_amount_and_leaves_the_expense_unchanged()
    {
        var expense = Create();

        Should.Throw<InvalidValueException>(() => expense.Update(_report, October15, "Hôtel", 0m, Fnac));

        expense.Description.ShouldBe("Taxi");
        expense.Amount.ShouldBe(10m);
    }

    [Fact]
    public void Update_refuses_another_report()
    {
        var expense = Create();
        var otherReport = ExpenseReport.Create(_user, new ReportMonth(2025, 10));

        Should.Throw<ArgumentException>(() => expense.Update(otherReport, October15, "Hôtel", 120m, Fnac));
    }

    [Fact]
    public void Delete_records_the_first_deletion_date_only()
    {
        var expense = Create();
        var first = new DateTimeOffset(2026, 10, 11, 9, 30, 0, TimeSpan.Zero);

        expense.Delete(first);
        expense.Delete(first.AddHours(1));

        expense.DeletedAt.ShouldBe(first);
    }

    private Expense Create(
        DateOnly? date = null,
        string description = "Taxi",
        decimal amount = 10m,
        int liveExpenseCount = 0)
    {
        return Expense.Create(_report, _user, liveExpenseCount, date ?? October15, description, amount, Fnac);
    }
}
