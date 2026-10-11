using System.Net.Http.Json;
using System.Text.Json;
using AcceptanceTests.Support;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class ApiDocumentationSteps(ApiDriver api)
{
    [Given("the API runs in the Development environment")]
    public void GivenTheApiRunsInTheDevelopmentEnvironment()
    {
        api.UseDevelopmentEnvironment();
    }

    [Then("the OpenAPI document lists the path {string}")]
    public async Task ThenTheOpenApiDocumentListsThePathAsync(string path)
    {
        var document = await api.Response.Content.ReadFromJsonAsync<JsonElement>();
        document.GetProperty("paths").TryGetProperty(path, out _).ShouldBeTrue($"the document has no path {path}");
    }

    [Then("the OpenAPI document declares the response {string} for {string} on {string}")]
    public async Task ThenTheOpenApiDocumentDeclaresTheResponseAsync(string status, string method, string path)
    {
        var document = await api.Response.Content.ReadFromJsonAsync<JsonElement>();
        var responses = document.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("responses");
        responses.TryGetProperty(status, out _).ShouldBeTrue($"{method} {path} declares no {status} response");
    }

    [Then("the OpenAPI document declares the query parameter {string} for {string} on {string}")]
    public async Task ThenTheOpenApiDocumentDeclaresTheQueryParameterAsync(string name, string method, string path)
    {
        var document = await api.Response.Content.ReadFromJsonAsync<JsonElement>();
        var parameters = document.GetProperty("paths").GetProperty(path).GetProperty(method).GetProperty("parameters");
        parameters.EnumerateArray()
            .ShouldContain(parameter => parameter.GetProperty("name").GetString() == name &&
                                        parameter.GetProperty("in").GetString() == "query",
                $"{method} {path} declares no query parameter {name}");
    }
}
