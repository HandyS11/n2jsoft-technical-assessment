using EndToEndTests.Support;
using Microsoft.Playwright;

namespace EndToEndTests;

public sealed class UsersPageTests(AppFixture fixture) : PageTest(fixture)
{
    [Fact]
    public async Task Opening_the_app_shows_the_seeded_users()
    {
        await Page.GotoAsync("/");

        await Assertions.Expect(Page).ToHaveURLAsync(new Uri(Fixture.AngularUrl, "users").ToString());
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new()
        {
            Name = "Utilisateurs"
        })).ToBeVisibleAsync();
        var users = Page.GetByRole(AriaRole.Listitem);
        await Assertions.Expect(users.Filter(new()
        {
            HasText = "Juste Leblanc"
        })).ToBeVisibleAsync();
        await Assertions.Expect(users.Filter(new()
        {
            HasText = "Marc Assin"
        })).ToBeVisibleAsync();
    }
}
