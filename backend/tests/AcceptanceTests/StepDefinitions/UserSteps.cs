using System.Net.Http.Json;
using AcceptanceTests.Support;
using Application.Users.Queries;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class UserSteps(ApiDriver api)
{
    private const string UsersPath = "/users";

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

    [Then("the users listed are")]
    public async Task ThenTheUsersListedAreAsync(DataTable table)
    {
        var users = await ReadUsersAsync();
        users.Select(user => user.Name).ShouldBe(table.Rows.Select(row => row["Name"]));
    }

    [Then("no user is listed")]
    public async Task ThenNoUserIsListedAsync()
    {
        var users = await ReadUsersAsync();
        users.ShouldBeEmpty();
    }

    private async Task<List<UserSummary>> ReadUsersAsync()
    {
        return (await api.Response.Content.ReadFromJsonAsync<List<UserSummary>>()).ShouldNotBeNull();
    }
}
