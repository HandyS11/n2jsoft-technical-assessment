using EndToEndTests.Support;

namespace EndToEndTests;

public sealed class AspireDashboardTests(AppFixture fixture)
{
    [Fact]
    public void The_webapi_resource_links_to_Scalar()
    {
        fixture.App.ResourceNotifications.TryGetCurrentState("webapi", out var resourceEvent).ShouldBeTrue();

        resourceEvent.ShouldNotBeNull().Snapshot.Urls
            .Where(url => url.DisplayProperties.DisplayName == "Scalar")
            .ShouldHaveSingleItem()
            .Url.ShouldEndWith("/scalar");
    }
}
