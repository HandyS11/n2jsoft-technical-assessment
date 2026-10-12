using Domain.Entities;
using Infrastructure.Persistence;

namespace UnitTests.Persistence;

public sealed class DemoDataTests
{
    private static readonly DateOnly October12 = new(2026, 10, 12);

    /// <summary>A month's first day, a year's last day, a leap day and a day in the middle of a month.</summary>
    public static TheoryData<DateOnly> Days =>
    [
        new DateOnly(2027, 1, 1),
        new DateOnly(2026, 12, 31),
        new DateOnly(2028, 2, 29),
        October12,
    ];

    [Fact]
    public void Create_fills_more_than_one_page_of_the_users_list()
    {
        // The lists show 20 rows a page.
        DemoData.Create(October12).Users.Count.ShouldBeGreaterThan(20);
    }

    [Fact]
    public void Create_includes_the_users_the_end_to_end_tests_look_for()
    {
        var names = DemoData.Create(October12).Users.Select(user => $"{user.FirstName} {user.LastName}").ToList();

        names.ShouldContain("Juste Leblanc");
        names.ShouldContain("Marc Assin");
    }

    [Fact]
    public void Create_returns_the_same_data_for_the_same_day()
    {
        Describe(DemoData.Create(October12)).ShouldBe(Describe(DemoData.Create(October12)));
    }

    [Theory]
    [InlineData(2027, 1, 1, 2026, 2)]
    [InlineData(2026, 10, 12, 2025, 11)]
    public void Create_covers_the_twelve_months_up_to_today(
        int year,
        int month,
        int day,
        int firstYear,
        int firstMonth)
    {
        var months = DemoData.Create(new DateOnly(year, month, day)).ExpenseReports
            .Select(report => (report.Year, report.Month))
            .Distinct()
            .Order()
            .ToList();

        months.Count.ShouldBe(12);
        months[0].ShouldBe((firstYear, firstMonth));
        months[^1].ShouldBe((year, month));
    }

    [Theory]
    [MemberData(nameof(Days))]
    public void Create_dates_no_expense_after_today(DateOnly today)
    {
        var expenses = DemoData.Create(today).Expenses;

        expenses.ShouldNotBeEmpty();
        expenses.ShouldAllBe(expense => expense.Date <= today);
    }

    [Fact]
    public void Create_keeps_every_report_within_the_quota_of_its_user()
    {
        var data = DemoData.Create(October12);
        var quotas = data.ExpenseReports.ToDictionary(report => report.Id,
            report => data.Users.Single(user => user.Id == report.UserId).MonthlyExpenseQuota);

        data.Expenses.ShouldNotBeEmpty();
        data.Expenses.GroupBy(expense => expense.ExpenseReportId)
            .ShouldAllBe(expenses => expenses.Count() <= quotas[expenses.Key]);
    }

    [Fact]
    public void Create_gives_a_user_at_most_one_report_a_month()
    {
        var reports = DemoData.Create(October12).ExpenseReports;

        reports.ShouldNotBeEmpty();
        reports.GroupBy(report => (report.UserId, report.Year, report.Month))
            .ShouldAllBe(sameMonth => sameMonth.Count() == 1);
    }

    [Fact]
    public void Create_fills_a_report_of_the_current_month_to_the_quota_of_its_user()
    {
        var data = DemoData.Create(October12);

        CurrentReports(data).ShouldContain(report =>
            ExpenseCount(data, report) == data.Users.Single(user => user.Id == report.UserId).MonthlyExpenseQuota);
    }

    [Fact]
    public void Create_leaves_a_report_of_the_current_month_empty()
    {
        var data = DemoData.Create(October12);

        CurrentReports(data).ShouldContain(report => ExpenseCount(data, report) == 0);
    }

    [Fact]
    public void Create_puts_more_than_one_batch_of_five_expenses_in_a_report_of_the_current_month()
    {
        var data = DemoData.Create(October12);

        CurrentReports(data).ShouldContain(report => ExpenseCount(data, report) > 5);
    }

    [Fact]
    public void Create_leaves_active_users_free_to_get_a_report_for_the_current_month()
    {
        var data = DemoData.Create(October12);
        var withCurrentReport = CurrentReports(data).Select(report => report.UserId).ToHashSet();

        data.Users.ShouldContain(user => user.IsActive && !withCurrentReport.Contains(user.Id));
    }

    [Fact]
    public void Create_deactivates_users_who_have_no_report_for_the_current_month()
    {
        var data = DemoData.Create(October12);
        var withCurrentReport = CurrentReports(data).Select(report => report.UserId).ToHashSet();
        var inactive = data.Users.Where(user => !user.IsActive).ToList();

        inactive.ShouldNotBeEmpty();
        inactive.ShouldAllBe(user => !withCurrentReport.Contains(user.Id));
    }

    private static List<ExpenseReport> CurrentReports(DemoData data)
    {
        return [.. data.ExpenseReports.Where(report => report.Year == 2026 && report.Month == 10)];
    }

    private static int ExpenseCount(DemoData data, ExpenseReport report)
    {
        return data.Expenses.Count(expense => expense.ExpenseReportId == report.Id);
    }

    /// <summary>Every detail but the ids, which differ from one call to the next.</summary>
    private static List<string> Describe(DemoData data)
    {
        var titles = data.ExpenseReports.ToDictionary(report => report.Id, report => report.Title);
        return
        [
            .. data.Users.Select(user =>
                $"{user.FirstName} {user.LastName} {user.Address} {user.MonthlyExpenseQuota} {user.IsActive}"),
            .. data.ExpenseReports.Select(report => report.Title),
            .. data.Expenses.Select(expense =>
                $"{titles[expense.ExpenseReportId]} {expense.Date} {expense.Description} {expense.Amount} {expense.BillingAddress}"),
        ];
    }
}
