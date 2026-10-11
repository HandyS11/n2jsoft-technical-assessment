using EndToEndTests.Support;
using Microsoft.Playwright;

namespace EndToEndTests;

public sealed class UsersPageTests(AppFixture fixture) : PageTest(fixture)
{
    [Fact]
    public async Task Opening_the_users_page_shows_the_seeded_users()
    {
        await Page.GotoAsync("/users");

        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new()
        {
            Name = "Utilisateurs"
        })).ToBeVisibleAsync();
        var users = Page.GetByRole(AriaRole.Row);
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
