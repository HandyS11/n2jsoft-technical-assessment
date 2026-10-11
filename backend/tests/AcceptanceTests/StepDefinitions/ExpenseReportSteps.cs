using System.Globalization;
using AcceptanceTests.Support;
using Application.ExpenseReports;
using Application.Paging;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class ExpenseReportSteps(ApiDriver api, KnownUsers knownUsers, KnownExpenseReports knownReports)
{
    private const string ExpenseReportsPath = "/v1/expense-reports";

    /// <summary>
    /// "User" names a user arranged earlier in the scenario, still active at this point (deactivate or delete them
    /// afterwards); "Month" is written "2025-10".
    /// </summary>
    [Given("the following expense reports exist")]
    public async Task GivenTheFollowingExpenseReportsExistAsync(DataTable table)
    {
        var reports = new List<ExpenseReport>();
        using (var scope = TestEnvironment.Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var row in table.Rows)
            {
                var userId = knownUsers.IdOf(row["User"]);
                var user = await dbContext.Set<User>().SingleAsync(stored => stored.Id == userId);
                reports.Add(ExpenseReport.Create(user, Months.Parse(row["Month"])));
            }

            dbContext.AddRange(reports);
            await dbContext.SaveChangesAsync();
        }

        foreach (var report in reports)
        {
            knownReports.Add(report.Title, report.Id);
        }
    }

    [When("I list the expense reports")]
    public Task WhenIListTheExpenseReportsAsync()
    {
        return api.GetAsync(ExpenseReportsPath);
    }

    [When("I list page {int} of the expense reports with {int} reports per page")]
    public Task WhenIListAPageOfTheExpenseReportsAsync(int page, int pageSize)
    {
        return api.GetAsync($"{ExpenseReportsPath}?page={page}&pageSize={pageSize}");
    }

    [When("I get the expense report {string}")]
    public Task WhenIGetTheExpenseReportAsync(string title)
    {
        return api.GetAsync($"{ExpenseReportsPath}/{knownReports.IdOf(title)}");
    }

    [When("I get an unknown expense report")]
    public Task WhenIGetAnUnknownExpenseReportAsync()
    {
        return api.GetAsync($"{ExpenseReportsPath}/{Guid.NewGuid()}");
    }

    [Then("the expense reports listed are")]
    public async Task ThenTheExpenseReportsListedAreAsync(DataTable table)
    {
        var reports = await api.ReadJsonAsync<PagedResult<ExpenseReportSummary>>();
        reports.Items
            .Select(report => (report.Title, Months.Format(report.Year, report.Month)))
            .ShouldBe(table.Rows.Select(row => (row["Title"], row["Month"])));
    }

    [Then("the expense reports listed with their expenses are")]
    public async Task ThenTheExpenseReportsListedWithTheirExpensesAreAsync(DataTable table)
    {
        var reports = await api.ReadJsonAsync<PagedResult<ExpenseReportSummary>>();
        reports.Items
            .Select(report => (report.Title, report.ExpenseCount, Amounts.Format(report.Total)))
            .ShouldBe(table.Rows.Select(row => (row["Title"],
                int.Parse(row["Expenses"], CultureInfo.InvariantCulture), row["Total"])));
    }

    [Then("no expense report is listed")]
    public async Task ThenNoExpenseReportIsListedAsync()
    {
        var reports = await api.ReadJsonAsync<PagedResult<ExpenseReportSummary>>();
        reports.Items.ShouldBeEmpty();
        reports.TotalCount.ShouldBe(0);
    }

    [Then("the listing reports {int} expense reports in total")]
    public async Task ThenTheListingReportsExpenseReportsInTotalAsync(int totalCount)
    {
        var reports = await api.ReadJsonAsync<PagedResult<ExpenseReportSummary>>();
        reports.TotalCount.ShouldBe(totalCount);
    }

    [Then("the expense report returned is")]
    public async Task ThenTheExpenseReportReturnedIsAsync(DataTable table)
    {
        var report = await api.ReadJsonAsync<ExpenseReportDetails>();
        var expected = table.Rows.ShouldHaveSingleItem();
        report.ShouldSatisfyAllConditions(
            () => report.Title.ShouldBe(expected["Title"]),
            () => report.UserId.ShouldBe(knownUsers.IdOf(expected["User"])),
            () => Months.Format(report.Year, report.Month).ShouldBe(expected["Month"]));
    }

    [Then("the expense report returned holds {int} expenses totalling {string}")]
    public async Task ThenTheExpenseReportReturnedHoldsExpensesTotallingAsync(int expenseCount, string total)
    {
        var report = await api.ReadJsonAsync<ExpenseReportDetails>();
        report.ExpenseCount.ShouldBe(expenseCount);
        Amounts.Format(report.Total).ShouldBe(total);
    }

    [Then("the expense report returned allows {int} expenses a month")]
    public async Task ThenTheExpenseReportReturnedAllowsExpensesAMonthAsync(int monthlyExpenseQuota)
    {
        var report = await api.ReadJsonAsync<ExpenseReportDetails>();
        report.MonthlyExpenseQuota.ShouldBe(monthlyExpenseQuota);
    }

    [Then("the expense report returned is read-only")]
    public async Task ThenTheExpenseReportReturnedIsReadOnlyAsync()
    {
        (await api.ReadJsonAsync<ExpenseReportDetails>()).IsReadOnly.ShouldBeTrue();
    }

    [Then("the expense report returned is not read-only")]
    public async Task ThenTheExpenseReportReturnedIsNotReadOnlyAsync()
    {
        (await api.ReadJsonAsync<ExpenseReportDetails>()).IsReadOnly.ShouldBeFalse();
    }

    [Then("the expense report {string} is no longer stored")]
    public async Task ThenTheExpenseReportIsNoLongerStoredAsync(string title)
    {
        var id = knownReports.IdOf(title);
        using var scope = TestEnvironment.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await dbContext.Set<ExpenseReport>().AnyAsync(report => report.Id == id)).ShouldBeFalse();
    }
}
