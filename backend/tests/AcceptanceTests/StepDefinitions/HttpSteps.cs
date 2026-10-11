using System.Net.Http.Json;
using System.Text.Json;
using AcceptanceTests.Support;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class HttpSteps(ApiDriver api)
{
    [When("I request {string}")]
    public Task WhenIRequestAsync(string path)
    {
        return api.GetAsync(path);
    }

    [Then("the response status is {int}")]
    public void ThenTheResponseStatusIs(int status)
    {
        ((int)api.Response.StatusCode).ShouldBe(status);
    }

    [Then("the response header {string} is {string}")]
    public void ThenTheResponseHeaderIs(string name, string value)
    {
        api.Response.Headers.GetValues(name).ShouldHaveSingleItem().ShouldBe(value);
    }

    [Then("the response is a problem with status {int}")]
    public async Task ThenTheResponseIsAProblemWithStatusAsync(int status)
    {
        ((int)api.Response.StatusCode).ShouldBe(status);
        api.Response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/problem+json");
        var problem = await api.Response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().ShouldBe(status);
    }

    [Then("the response is a business rule violation with the code {string}")]
    public async Task ThenTheResponseIsABusinessRuleViolationWithTheCodeAsync(string code)
    {
        ((int)api.Response.StatusCode).ShouldBe(422);
        api.Response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/problem+json");
        // The buffered string can be read again by a later step; the stream cannot.
        using var problem = JsonDocument.Parse(await api.Response.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("code").GetString().ShouldBe(code);
    }
}
