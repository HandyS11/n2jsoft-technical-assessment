using AcceptanceTests.Support;
using Application.Expenses;
using Application.Paging;
using Domain.Entities;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class ExpenseSteps(ApiDriver api, KnownExpenseReports knownReports, KnownExpenses knownExpenses)
{
    /// <summary>
    /// Stored as they are, without the quota: a scenario can fill a report up to its quota. "Date" is written
    /// "2025-10-15" and must fall in the report's month, "Description" is required and unique in the scenario.
    /// "Amount" (10.00), "Store" (none), "Street", "Postal code" and "City" default to valid values; "Status" is
    /// Live (default) or Deleted. The report's user must still be active or inactive, not deleted.
    /// </summary>
    [Given("the following expenses exist in {string}")]
    public async Task GivenTheFollowingExpensesExistInAsync(string title, DataTable table)
    {
        var reportId = knownReports.IdOf(title);
        var expenses = new List<Expense>();
        using (var scope = TestEnvironment.Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var report = await dbContext.Set<ExpenseReport>().SingleAsync(stored => stored.Id == reportId);
            var user = await dbContext.Set<User>().SingleAsync(stored => stored.Id == report.UserId);
            foreach (var row in table.Rows)
            {
                var expense = Expense.Create(
                    report,
                    user,
                    0,
                    Dates.Parse(row["Date"]),
                    row["Description"],
                    Amounts.Parse(Optional(row, "Amount", "10.00")),
                    new BillingAddress(
                        row.TryGetValue("Store", out var store) ? store : null,
                        new Address(
                            Optional(row, "Street", "1 rue de la Paix"),
                            Optional(row, "Postal code", "75002"),
                            Optional(row, "City", "Paris"))));
                if (Optional(row, "Status", "Live") == "Deleted")
                {
                    expense.Delete(DateTimeOffset.UtcNow);
                }

                expenses.Add(expense);
            }

            dbContext.AddRange(expenses);
            await dbContext.SaveChangesAsync();
        }

        foreach (var expense in expenses)
        {
            knownExpenses.Add(expense.Description, reportId, expense.Id);
        }
    }

    [When("I list the expenses of {string}")]
    public Task WhenIListTheExpensesOfAsync(string title)
    {
        return api.GetAsync(ExpensePaths.Of(knownReports.IdOf(title)));
    }

    [When("I list page {int} of the expenses of {string} with {int} expenses per page")]
    public Task WhenIListAPageOfTheExpensesOfAsync(int page, string title, int pageSize)
    {
        return api.GetAsync($"{ExpensePaths.Of(knownReports.IdOf(title))}?page={page}&pageSize={pageSize}");
    }

    [When("I list the expenses of an unknown expense report")]
    public Task WhenIListTheExpensesOfAnUnknownExpenseReportAsync()
    {
        return api.GetAsync(ExpensePaths.Of(Guid.NewGuid()));
    }

    [When("I get the expense {string}")]
    public Task WhenIGetTheExpenseAsync(string description)
    {
        var (reportId, id) = knownExpenses.Of(description);
        return api.GetAsync(ExpensePaths.Of(reportId, id));
    }

    /// <summary>The right expense id, under the wrong report.</summary>
    [When("I get the expense {string} through the report {string}")]
    public Task WhenIGetTheExpenseThroughTheReportAsync(string description, string title)
    {
        return api.GetAsync(ExpensePaths.Of(knownReports.IdOf(title), knownExpenses.Of(description).Id));
    }

    [When("I get an unknown expense of {string}")]
    public Task WhenIGetAnUnknownExpenseOfAsync(string title)
    {
        return api.GetAsync(ExpensePaths.Of(knownReports.IdOf(title), Guid.NewGuid()));
    }

    [Then("the expenses listed are")]
    public async Task ThenTheExpensesListedAreAsync(DataTable table)
    {
        var expenses = await api.ReadJsonAsync<PagedResult<ExpenseSummary>>();
        expenses.Items
            .Select(expense => (Dates.Format(expense.Date), expense.Description, Amounts.Format(expense.Amount)))
            .ShouldBe(table.Rows.Select(row => (row["Date"], row["Description"], row["Amount"])));
    }

    [Then("no expense is listed")]
    public async Task ThenNoExpenseIsListedAsync()
    {
        var expenses = await api.ReadJsonAsync<PagedResult<ExpenseSummary>>();
        expenses.Items.ShouldBeEmpty();
        expenses.TotalCount.ShouldBe(0);
    }

    [Then("the listing reports {int} expenses in total")]
    public async Task ThenTheListingReportsExpensesInTotalAsync(int totalCount)
    {
        var expenses = await api.ReadJsonAsync<PagedResult<ExpenseSummary>>();
        expenses.TotalCount.ShouldBe(totalCount);
    }

    /// <summary>"Store" is empty for an expense without one.</summary>
    [Then("the expense returned is")]
    public async Task ThenTheExpenseReturnedIsAsync(DataTable table)
    {
        var expense = await api.ReadJsonAsync<ExpenseDetails>();
        var expected = table.Rows.ShouldHaveSingleItem();
        expense.ShouldSatisfyAllConditions(
            () => Dates.Format(expense.Date).ShouldBe(expected["Date"]),
            () => expense.Description.ShouldBe(expected["Description"]),
            () => Amounts.Format(expense.Amount).ShouldBe(expected["Amount"]),
            () => (expense.BillingAddress.StoreName ?? string.Empty).ShouldBe(expected["Store"]),
            () => expense.BillingAddress.Street.ShouldBe(expected["Street"]),
            () => expense.BillingAddress.PostalCode.ShouldBe(expected["Postal code"]),
            () => expense.BillingAddress.City.ShouldBe(expected["City"]));
    }

    [Then("the response ETag is the version of the expense returned")]
    public async Task ThenTheResponseETagIsTheVersionOfTheExpenseReturnedAsync()
    {
        var expense = await api.ReadJsonAsync<ExpenseDetails>();
        // The database gives the version (xmin) when it saves the expense: 0 means it was not read back.
        expense.Version.ShouldNotBe(0u);
        api.Response.Headers.ETag.ShouldNotBeNull().ToString().ShouldBe($"\"{expense.Version}\"");
    }

    [Then("the expense {string} is still stored, marked as deleted")]
    public async Task ThenTheExpenseIsStillStoredMarkedAsDeletedAsync(string description)
    {
        var id = knownExpenses.Of(description).Id;
        using var scope = TestEnvironment.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expense = await dbContext.Set<Expense>().IgnoreQueryFilters().SingleAsync(stored => stored.Id == id);
        expense.DeletedAt.ShouldNotBeNull();
    }

    [Then("no expense of {string} is stored anymore, deleted ones included")]
    public async Task ThenNoExpenseOfTheReportIsStoredAnymoreAsync(string title)
    {
        var reportId = knownReports.IdOf(title);
        using var scope = TestEnvironment.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await dbContext.Set<Expense>().IgnoreQueryFilters().AnyAsync(expense => expense.ExpenseReportId == reportId))
            .ShouldBeFalse();
    }

    private static string Optional(DataTableRow row, string column, string defaultValue)
    {
        return row.TryGetValue(column, out var value) ? value : defaultValue;
    }
}
