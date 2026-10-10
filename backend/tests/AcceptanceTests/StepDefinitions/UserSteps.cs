using System.Text.Json;
using AcceptanceTests.Support;
using Application.Paging;
using Application.Users.Queries;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class UserSteps(ApiDriver api)
{
    private const string UsersPath = "/v1/users";

    [Given("the following users exist")]
    public static async Task GivenTheFollowingUsersExistAsync(DataTable table)
    {
        using var scope = TestEnvironment.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.AddRange(table.Rows.Select(row => new User
        {
            FirstName = row["First name"], LastName = row["Last name"],
        }));
        await dbContext.SaveChangesAsync();
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

    [Then("the users listed are")]
    public async Task ThenTheUsersListedAreAsync(DataTable table)
    {
        var users = await ReadUsersAsync();
        users.Items.Select(user => user.Name).ShouldBe(table.Rows.Select(row => row["Name"]));
    }

    [Then("no user is listed")]
    public async Task ThenNoUserIsListedAsync()
    {
        var users = await ReadUsersAsync();
        users.Items.ShouldBeEmpty();
        users.TotalCount.ShouldBe(0);
    }

    [Then("the listing reports {int} users in total")]
    public async Task ThenTheListingReportsUsersInTotalAsync(int totalCount)
    {
        var users = await ReadUsersAsync();
        users.TotalCount.ShouldBe(totalCount);
    }

    /// <summary>Several Then steps read the same response: the buffered string can be read again, the stream cannot.</summary>
    private async Task<PagedResult<UserSummary>> ReadUsersAsync()
    {
        var json = await api.Response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<PagedResult<UserSummary>>(json, JsonSerializerOptions.Web).ShouldNotBeNull();
    }
}
