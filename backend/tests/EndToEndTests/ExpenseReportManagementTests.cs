using System.Text.RegularExpressions;
using EndToEndTests.Support;
using Microsoft.Playwright;

namespace EndToEndTests;

public sealed partial class ExpenseReportManagementTests(AppFixture fixture) : PageTest(fixture)
{
    [Fact]
    public async Task Opening_the_app_shows_the_expense_reports()
    {
        await Page.GotoAsync("/");

        await Assertions.Expect(Page).ToHaveURLAsync(new Uri(Fixture.AngularUrl, "expense-reports").ToString());
        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new()
        {
            Name = "Notes de frais", Exact = true
        })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task An_administrator_creates_a_report_for_the_current_month_and_opens_it()
    {
        var name = await CreateUserAsync("Jeanne");

        await CreateReportAndOpenItAsync(name);

        await Assertions.Expect(Page.GetByRole(AriaRole.Heading, new()
        {
            Level = 1
        })).ToContainTextAsync($"{name} - ");
        await Assertions.Expect(Page.GetByText("Mois :")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task An_administrator_is_told_when_the_report_already_exists()
    {
        var name = await CreateUserAsync("Paul");
        await CreateReportAndOpenItAsync(name);

        await CreateReportAsync(name);

        await Assertions.Expect(Page.GetByRole(AriaRole.Alert))
            .ToHaveTextAsync("Une note de frais existe déjà pour cet utilisateur et ce mois.");
    }

    [Fact]
    public async Task An_administrator_deletes_a_report_from_the_list()
    {
        var name = await CreateUserAsync("Lucie");
        await CreateReportAndOpenItAsync(name);
        await Page.GetByRole(AriaRole.Link, new()
        {
            Name = "← Notes de frais"
        }).ClickAsync();
        var row = await FindRowAsync(name);
        await Assertions.Expect(row).ToHaveCountAsync(1);

        Page.Dialog += AcceptConfirmation;
        await row.GetByRole(AriaRole.Button, new()
        {
            Name = "Supprimer"
        }).ClickAsync();

        await Assertions.Expect(row).ToHaveCountAsync(0);
    }

    [GeneratedRegex("/expense-reports/[0-9a-f-]{36}$")]
    private static partial Regex ReportPageUrl();

    /// <summary>Playwright dismisses the dialogs nobody handles: accept the deletion confirmation.</summary>
    private static void AcceptConfirmation(object? sender, IDialog dialog)
    {
        _ = dialog.AcceptAsync();
    }

    /// <summary>
    /// Creates an active user with a unique last name through the API (the users journeys already cover the form)
    /// and returns their full name.
    /// </summary>
    private async Task<string> CreateUserAsync(string firstName)
    {
        var lastName = $"E2e{Guid.NewGuid().ToString("N")[..8]}";
        var response = await Page.APIRequest.PostAsync("/api/v1/users", new()
        {
            DataObject = new
            {
                firstName,
                lastName,
                address = new
                {
                    street = "5 rue Victor Hugo", postalCode = "33000", city = "Bordeaux"
                },
                monthlyExpenseQuota = 8,
            },
        });
        response.Ok.ShouldBeTrue($"creating the user answered {response.Status}");
        return $"{firstName} {lastName}";
    }

    /// <summary>Creates a report for the current month (the form's default) from the reports page.</summary>
    private async Task CreateReportAsync(string name)
    {
        await Page.GotoAsync("/expense-reports");
        await Page.GetByLabel("Utilisateur", new()
        {
            Exact = true
        }).SelectOptionAsync(new SelectOptionValue
        {
            Label = name
        });
        await Page.GetByRole(AriaRole.Button, new()
        {
            Name = "Créer"
        }).ClickAsync();
    }

    private async Task CreateReportAndOpenItAsync(string name)
    {
        await CreateReportAsync(name);
        await Assertions.Expect(Page).ToHaveURLAsync(ReportPageUrl());
    }
}
