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
}
