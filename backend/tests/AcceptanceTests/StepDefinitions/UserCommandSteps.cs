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
        return CreateAsync(With(ValidRequest(), field, json));
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
        return api.SendAsync(HttpMethod.Put, PathOf(knownUsers.IdOf(name)), With(ValidRequest(), field, json));
    }

    [When("I update an unknown user")]
    public Task WhenIUpdateAnUnknownUserAsync()
    {
        return api.SendAsync(HttpMethod.Put, PathOf(Guid.NewGuid()), ValidRequest());
    }

    [Then("the response locates the created user")]
    public async Task ThenTheResponseLocatesTheCreatedUserAsync()
    {
        var user = await api.ReadJsonAsync<UserDetails>();
        api.Response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe(PathOf(user.Id));
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

    /// <summary>Replaces the field at <paramref name="path" /> ("address.city") with the parsed JSON value.</summary>
    private static JsonObject With(JsonObject request, string path, string json)
    {
        var segments = path.Split('.');
        var parent = segments[..^1].Aggregate(request, (node, segment) => node[segment]!.AsObject());
        parent[segments[^1]] = JsonNode.Parse(json);
        return request;
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
