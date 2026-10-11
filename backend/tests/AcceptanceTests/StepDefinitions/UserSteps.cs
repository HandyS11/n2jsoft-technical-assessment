using System.Globalization;
using AcceptanceTests.Support;
using Application.Paging;
using Application.Users;
using Domain.Entities;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class UserSteps(ApiDriver api, KnownUsers knownUsers)
{
    private const string UsersPath = "/v1/users";

    /// <summary>
    /// "First name" and "Last name" are required. "Street", "Postal code", "City" and "Monthly quota" default to valid
    /// values; "Status" is Active (default), Inactive or Deleted.
    /// </summary>
    [Given("the following users exist")]
    public async Task GivenTheFollowingUsersExistAsync(DataTable table)
    {
        var users = table.Rows.Select(ToUser).ToList();
        using (var scope = TestEnvironment.Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.AddRange(users);
            await dbContext.SaveChangesAsync();
        }

        foreach (var user in users)
        {
            knownUsers.Add($"{user.FirstName} {user.LastName}", user.Id);
        }
    }

    [When("I list the users")]
    public Task WhenIListTheUsersAsync()
    {
        return api.GetAsync(UsersPath);
    }

    [When("I list page {int} of the users with {int} users per page")]
    public Task WhenIListAPageOfTheUsersAsync(int page, int pageSize)
    {
        return api.GetAsync($"{UsersPath}?page={page}&pageSize={pageSize}");
    }

    [When("I get the user {string}")]
    public Task WhenIGetTheUserAsync(string name)
    {
        return api.GetAsync($"{UsersPath}/{knownUsers.IdOf(name)}");
    }

    [When("I get an unknown user")]
    public Task WhenIGetAnUnknownUserAsync()
    {
        return api.GetAsync($"{UsersPath}/{Guid.NewGuid()}");
    }

    [Then("the users listed are")]
    public async Task ThenTheUsersListedAreAsync(DataTable table)
    {
        var users = await api.ReadJsonAsync<PagedResult<UserSummary>>();
        users.Items.Select(user => user.Name).ShouldBe(table.Rows.Select(row => row["Name"]));
    }

    [Then("the users listed with their details are")]
    public async Task ThenTheUsersListedWithTheirDetailsAreAsync(DataTable table)
    {
        var users = await api.ReadJsonAsync<PagedResult<UserSummary>>();
        users.Items
            .Select(user => (user.Name, user.City, user.MonthlyExpenseQuota, StatusOf(user.IsActive)))
            .ShouldBe(table.Rows.Select(row =>
                (row["Name"], row["City"], int.Parse(row["Monthly quota"], CultureInfo.InvariantCulture),
                    row["Status"])));
    }

    [Then("no user is listed")]
    public async Task ThenNoUserIsListedAsync()
    {
        var users = await api.ReadJsonAsync<PagedResult<UserSummary>>();
        users.Items.ShouldBeEmpty();
        users.TotalCount.ShouldBe(0);
    }

    [Then("the listing reports {int} users in total")]
    public async Task ThenTheListingReportsUsersInTotalAsync(int totalCount)
    {
        var users = await api.ReadJsonAsync<PagedResult<UserSummary>>();
        users.TotalCount.ShouldBe(totalCount);
    }

    [Then("the user returned is")]
    public async Task ThenTheUserReturnedIsAsync(DataTable table)
    {
        var user = await api.ReadJsonAsync<UserDetails>();
        var expected = table.Rows.ShouldHaveSingleItem();
        user.ShouldSatisfyAllConditions(
            () => user.FirstName.ShouldBe(expected["First name"]),
            () => user.LastName.ShouldBe(expected["Last name"]),
            () => user.Address.Street.ShouldBe(expected["Street"]),
            () => user.Address.PostalCode.ShouldBe(expected["Postal code"]),
            () => user.Address.City.ShouldBe(expected["City"]),
            () => user.MonthlyExpenseQuota.ShouldBe(int.Parse(expected["Monthly quota"], CultureInfo.InvariantCulture)),
            () => StatusOf(user.IsActive).ShouldBe(expected["Status"]));
    }

    private static User ToUser(DataTableRow row)
    {
        var user = User.Create(
            row["First name"],
            row["Last name"],
            new Address(
                Optional(row, "Street", "1 rue de la Paix"),
                Optional(row, "Postal code", "75002"),
                Optional(row, "City", "Paris")),
            int.Parse(Optional(row, "Monthly quota", "10"), CultureInfo.InvariantCulture));

        switch (Optional(row, "Status", "Active"))
        {
            case "Active":
                break;
            case "Inactive":
                user.Deactivate();
                break;
            case "Deleted":
                user.Delete(DateTimeOffset.UtcNow);
                break;
            default:
                throw new ArgumentException($"Unknown status {row["Status"]}: use Active, Inactive or Deleted.",
                    nameof(row));
        }

        return user;
    }

    private static string Optional(DataTableRow row, string column, string defaultValue)
    {
        return row.TryGetValue(column, out var value) ? value : defaultValue;
    }

    private static string StatusOf(bool isActive)
    {
        return isActive ? "Active" : "Inactive";
    }
}
