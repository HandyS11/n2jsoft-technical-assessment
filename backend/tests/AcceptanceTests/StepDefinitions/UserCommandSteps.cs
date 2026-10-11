using System.Globalization;
using System.Text.Json.Nodes;
using AcceptanceTests.Support;
using Application.Users;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class UserCommandSteps(ApiDriver api, KnownUsers knownUsers)
{
    private const string UsersPath = "/v1/users";

    private string? _entityTagRead;

    [When("I create the user")]
    public Task WhenICreateTheUserAsync(DataTable table)
    {
        return CreateAsync(ToRequest(table.Rows.ShouldHaveSingleItem()));
    }

    /// <summary>
    /// Sends a valid request in which one field, given as a dotted JSON path ("address.postalCode"), is replaced by a
    /// JSON value ("", "  ", null, 0). Gherkin trims table cells, so padded values are written this way.
    /// </summary>
    [When(@"^I create a user whose (\S+) is (.+)$")]
    public Task WhenICreateAUserWhoseFieldIsAsync(string field, string json)
    {
        return CreateAsync(JsonRequests.With(ValidRequest(), field, json));
    }

    [When("I update the user {string} with")]
    public Task WhenIUpdateTheUserWithAsync(string name, DataTable table)
    {
        return api.SendAsync(HttpMethod.Put, PathOf(knownUsers.IdOf(name)),
            ToRequest(table.Rows.ShouldHaveSingleItem()));
    }

    [When(@"^I update the user ""([^""]+)"" whose (\S+) is (.+)$")]
    public Task WhenIUpdateTheUserWhoseFieldIsAsync(string name, string field, string json)
    {
        return api.SendAsync(HttpMethod.Put, PathOf(knownUsers.IdOf(name)),
            JsonRequests.With(ValidRequest(), field, json));
    }

    [When("I update an unknown user")]
    public Task WhenIUpdateAnUnknownUserAsync()
    {
        return api.SendAsync(HttpMethod.Put, PathOf(Guid.NewGuid()), ValidRequest());
    }

    [When("I update the user {string}")]
    public Task WhenIUpdateTheUserAsync(string name)
    {
        return api.SendAsync(HttpMethod.Put, PathOf(knownUsers.IdOf(name)), ValidRequest());
    }

    [When("I read the user {string}")]
    public async Task WhenIReadTheUserAsync(string name)
    {
        await api.GetAsync(PathOf(knownUsers.IdOf(name)));
        _entityTagRead = api.Response.Headers.ETag.ShouldNotBeNull().ToString();
    }

    [When("I update the user {string} with the ETag read")]
    public Task WhenIUpdateTheUserWithTheETagReadAsync(string name)
    {
        return api.SendAsync(HttpMethod.Put, PathOf(knownUsers.IdOf(name)), ValidRequest(),
            _entityTagRead.ShouldNotBeNull());
    }

    [When("I update the user {string} with the If-Match {string}")]
    public Task WhenIUpdateTheUserWithTheIfMatchAsync(string name, string ifMatch)
    {
        return api.SendAsync(HttpMethod.Put, PathOf(knownUsers.IdOf(name)), ValidRequest(), ifMatch);
    }

    [When("I activate the user {string}")]
    public Task WhenIActivateTheUserAsync(string name)
    {
        return api.SendAsync(HttpMethod.Post, $"{PathOf(knownUsers.IdOf(name))}/activate");
    }

    [When("I deactivate the user {string}")]
    public Task WhenIDeactivateTheUserAsync(string name)
    {
        return api.SendAsync(HttpMethod.Post, $"{PathOf(knownUsers.IdOf(name))}/deactivate");
    }

    [When("I delete the user {string}")]
    public Task WhenIDeleteTheUserAsync(string name)
    {
        return api.SendAsync(HttpMethod.Delete, PathOf(knownUsers.IdOf(name)));
    }

    [When("I activate an unknown user")]
    public Task WhenIActivateAnUnknownUserAsync()
    {
        return api.SendAsync(HttpMethod.Post, $"{PathOf(Guid.NewGuid())}/activate");
    }

    [When("I deactivate an unknown user")]
    public Task WhenIDeactivateAnUnknownUserAsync()
    {
        return api.SendAsync(HttpMethod.Post, $"{PathOf(Guid.NewGuid())}/deactivate");
    }

    [When("I delete an unknown user")]
    public Task WhenIDeleteAnUnknownUserAsync()
    {
        return api.SendAsync(HttpMethod.Delete, PathOf(Guid.NewGuid()));
    }

    [Then("the response locates the created user")]
    public async Task ThenTheResponseLocatesTheCreatedUserAsync()
    {
        var user = await api.ReadJsonAsync<UserDetails>();
        api.Response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe(PathOf(user.Id));
    }

    [Then("the response ETag is the version of the user returned")]
    public async Task ThenTheResponseETagIsTheVersionOfTheUserReturnedAsync()
    {
        var user = await api.ReadJsonAsync<UserDetails>();
        // The database gives the version (xmin) when it saves the user: 0 means it was not read back.
        user.Version.ShouldNotBe(0u);
        api.Response.Headers.ETag.ShouldNotBeNull().ToString().ShouldBe($"\"{user.Version}\"");
    }

    private static string PathOf(Guid id)
    {
        return $"{UsersPath}/{id}";
    }

    private static JsonObject ValidRequest()
    {
        return new JsonObject
        {
            ["firstName"] = "Jeanne",
            ["lastName"] = "Martin",
            ["address"] = new JsonObject
            {
                ["street"] = "5 rue Victor Hugo", ["postalCode"] = "33000", ["city"] = "Bordeaux",
            },
            ["monthlyExpenseQuota"] = 8,
        };
    }

    private static JsonObject ToRequest(DataTableRow row)
    {
        return new JsonObject
        {
            ["firstName"] = row["First name"],
            ["lastName"] = row["Last name"],
            ["address"] = new JsonObject
            {
                ["street"] = row["Street"], ["postalCode"] = row["Postal code"], ["city"] = row["City"],
            },
            ["monthlyExpenseQuota"] = int.Parse(row["Monthly quota"], CultureInfo.InvariantCulture),
        };
    }

    private async Task CreateAsync(JsonObject request)
    {
        await api.SendAsync(HttpMethod.Post, UsersPath, request);
        if (api.Response.IsSuccessStatusCode)
        {
            var user = await api.ReadJsonAsync<UserDetails>();
            knownUsers.Add($"{user.FirstName} {user.LastName}", user.Id);
        }
    }
}
