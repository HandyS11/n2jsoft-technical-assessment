using EndToEndTests.Support;
using Microsoft.Playwright;

namespace EndToEndTests;

public sealed class UsersPageTests(AppFixture fixture) : PageTest(fixture)
{
    [Fact]
    public async Task Opening_the_users_page_shows_the_seeded_users_over_several_pages()
    {
        await Page.GotoAsync("/users");

        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new()
        {
            Name = "Utilisateurs"
        })).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText("Page 1 sur")).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Button, new()
        {
            Name = "Suivant"
        })).ToBeEnabledAsync();
        await Assertions.Expect(await FindRowAsync("Marc Assin")).ToBeVisibleAsync();

        await Page.GotoAsync("/users");
        await Assertions.Expect(await FindRowAsync("Juste Leblanc")).ToBeVisibleAsync();
    }
}
