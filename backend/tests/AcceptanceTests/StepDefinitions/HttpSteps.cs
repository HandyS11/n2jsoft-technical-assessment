using AcceptanceTests.Support;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class HttpSteps(ApiDriver api)
{
    [Then("the response status is {int}")]
    public void ThenTheResponseStatusIs(int status)
    {
        ((int)api.Response.StatusCode).ShouldBe(status);
    }
}
