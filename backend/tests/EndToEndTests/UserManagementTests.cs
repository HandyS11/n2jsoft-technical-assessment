using EndToEndTests.Support;
using Microsoft.Playwright;

namespace EndToEndTests;

public sealed class UserManagementTests(AppFixture fixture) : PageTest(fixture)
{
    [Fact]
    public async Task An_administrator_creates_a_user_and_finds_them_in_the_list()
    {
        var name = await CreateUserAsync("Jeanne", "Bordeaux");

        var row = UserRow(name);
        await Assertions.Expect(row).ToContainTextAsync("Bordeaux");
        await Assertions.Expect(row).ToContainTextAsync("Actif");
    }

    [Fact]
    public async Task An_administrator_changes_the_city_of_a_user()
    {
        var name = await CreateUserAsync("Paul", "Bordeaux");

        await UserRow(name).GetByRole(AriaRole.Link, new()
        {
            Name = "Modifier"
        }).ClickAsync();
        var city = Page.GetByLabel("Ville");
        // The form is filled once the user is loaded.
        await Assertions.Expect(city).ToHaveValueAsync("Bordeaux");
        await city.FillAsync("Toulouse");
        await SaveAsync();

        await Assertions.Expect(UserRow(name)).ToContainTextAsync("Toulouse");
    }

    [Fact]
    public async Task An_administrator_deactivates_then_deletes_a_user()
    {
        var name = await CreateUserAsync("Lucie", "Nantes");
        var row = UserRow(name);

        await row.GetByRole(AriaRole.Button, new()
        {
            Name = "Désactiver"
        }).ClickAsync();
        await Assertions.Expect(row).ToContainTextAsync("Inactif");

        Page.Dialog += AcceptConfirmation;
        await row.GetByRole(AriaRole.Button, new()
        {
            Name = "Supprimer"
        }).ClickAsync();

        await Assertions.Expect(row).ToHaveCountAsync(0);
    }

    /// <summary>Playwright dismisses the dialogs nobody handles: accept the deletion confirmation.</summary>
    private static void AcceptConfirmation(object? sender, IDialog dialog)
    {
        _ = dialog.AcceptAsync();
    }

    /// <summary>Creates a user with a unique last name through the form and returns their full name.</summary>
    private async Task<string> CreateUserAsync(string firstName, string city)
    {
        var lastName = $"E2e{Guid.NewGuid().ToString("N")[..8]}";
        await Page.GotoAsync("/users");
        await Page.GetByRole(AriaRole.Link, new()
        {
            Name = "Nouvel utilisateur"
        }).ClickAsync();
        await Page.GetByLabel("Prénom").FillAsync(firstName);
        await Page.GetByLabel("Nom", new()
        {
            Exact = true
        }).FillAsync(lastName);
        await Page.GetByLabel("Rue").FillAsync("5 rue Victor Hugo");
        await Page.GetByLabel("Code postal").FillAsync("33000");
        await Page.GetByLabel("Ville").FillAsync(city);
        await Page.GetByLabel("Quota mensuel de dépenses").FillAsync("8");
        await SaveAsync();
        return $"{firstName} {lastName}";
    }

    private async Task SaveAsync()
    {
        await Page.GetByRole(AriaRole.Button, new()
        {
            Name = "Enregistrer"
        }).ClickAsync();
        await Assertions.Expect(Page).ToHaveURLAsync(new Uri(Fixture.AngularUrl, "users").ToString());
    }

    private ILocator UserRow(string name)
    {
        return Page.GetByRole(AriaRole.Row).Filter(new()
        {
            HasText = name
        });
    }
}
