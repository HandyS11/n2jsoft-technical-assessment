using System.Text.Json;
using EndToEndTests.Support;
using Microsoft.Playwright;

namespace EndToEndTests;

/// <summary>
/// Each journey creates its own user (quota 2) and their October 2025 report through the API, then works on the
/// report page. A fixed month keeps the expected French date label independent of the day the tests run.
/// </summary>
public sealed class ExpenseManagementTests(AppFixture fixture) : PageTest(fixture)
{
    private const int Quota = 2;

    [Fact]
    public async Task An_administrator_adds_an_expense_and_sees_its_french_date()
    {
        var reportId = await CreateReportAsync("Hugo");
        await Page.GotoAsync($"/expense-reports/{reportId}");

        await AddExpenseFromTheFormAsync("2025-10-15", "Taxi gare", "23.5");

        await Assertions.Expect(Page.GetByRole(AriaRole.Status)).ToHaveTextAsync("Dépense ajoutée.");
        var row = Page.GetByRole(AriaRole.Row).Filter(new()
        {
            HasText = "Taxi gare"
        });
        await Assertions.Expect(row).ToContainTextAsync("mercredi 15 octobre 2025");
        await Assertions.Expect(row).ToContainTextAsync("23,50 €");
        await Assertions.Expect(Page.GetByText("1 dépense sur 2 ce mois-ci")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task An_administrator_edits_the_amount_of_an_expense()
    {
        var reportId = await CreateReportAsync("Inès");
        await AddExpenseThroughTheApiAsync(reportId, "2025-10-03", "Repas client");
        await Page.GotoAsync($"/expense-reports/{reportId}");
        var row = Page.GetByRole(AriaRole.Row).Filter(new()
        {
            HasText = "Repas client"
        });

        await row.GetByRole(AriaRole.Button, new()
        {
            Name = "Modifier"
        }).ClickAsync();
        await Assertions.Expect(Page.GetByLabel("Description", new()
        {
            Exact = true
        })).ToHaveValueAsync("Repas client");
        await Page.GetByLabel("Montant (€)").FillAsync("42");
        await Page.GetByRole(AriaRole.Button, new()
        {
            Name = "Enregistrer"
        }).ClickAsync();

        await Assertions.Expect(Page.GetByRole(AriaRole.Status)).ToHaveTextAsync("Dépense modifiée.");
        await Assertions.Expect(row).ToContainTextAsync("42,00 €");
        await Assertions.Expect(Page.GetByText("Total : 42,00 €")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task An_administrator_is_told_when_the_monthly_quota_is_reached()
    {
        var reportId = await CreateReportAsync("Jules");
        for (var day = 1; day <= Quota; day++)
        {
            await AddExpenseThroughTheApiAsync(reportId, $"2025-10-0{day}", $"Péage {day}");
        }

        await Page.GotoAsync($"/expense-reports/{reportId}");
        await AddExpenseFromTheFormAsync("2025-10-20", "Parking", "8");

        await Assertions.Expect(Page.GetByRole(AriaRole.Alert)).ToHaveTextAsync(
            "Le quota mensuel de dépenses de cet utilisateur est atteint : impossible d'ajouter cette dépense.");
    }

    [Fact]
    public async Task Deleting_an_expense_frees_a_slot_of_the_quota()
    {
        var reportId = await CreateReportAsync("Karim");
        await AddExpenseThroughTheApiAsync(reportId, "2025-10-01", "Hôtel");
        await AddExpenseThroughTheApiAsync(reportId, "2025-10-02", "Train");
        await Page.GotoAsync($"/expense-reports/{reportId}");
        var row = Page.GetByRole(AriaRole.Row).Filter(new()
        {
            HasText = "Hôtel"
        });

        Page.Dialog += AcceptConfirmation;
        await row.GetByRole(AriaRole.Button, new()
        {
            Name = "Supprimer"
        }).ClickAsync();

        await Assertions.Expect(row).ToHaveCountAsync(0);
        await AddExpenseFromTheFormAsync("2025-10-20", "Parking", "8");
        await Assertions.Expect(Page.GetByRole(AriaRole.Status)).ToHaveTextAsync("Dépense ajoutée.");
    }

    /// <summary>Playwright dismisses the dialogs nobody handles: accept the deletion confirmation.</summary>
    private static void AcceptConfirmation(object? sender, IDialog dialog)
    {
        _ = dialog.AcceptAsync();
    }

    private async Task AddExpenseFromTheFormAsync(string date, string description, string amount)
    {
        await Page.GetByLabel("Date", new()
        {
            Exact = true
        }).FillAsync(date);
        await Page.GetByLabel("Description", new()
        {
            Exact = true
        }).FillAsync(description);
        await Page.GetByLabel("Montant (€)").FillAsync(amount);
        await Page.GetByLabel("Rue").FillAsync("8 quai du Port");
        await Page.GetByLabel("Code postal").FillAsync("13002");
        await Page.GetByLabel("Ville").FillAsync("Marseille");
        await Page.GetByRole(AriaRole.Button, new()
        {
            Name = "Ajouter"
        }).ClickAsync();
    }

    /// <summary>Creates an active user with a unique last name and their October 2025 report; returns the report id.</summary>
    private async Task<string> CreateReportAsync(string firstName)
    {
        var lastName = $"E2e{Guid.NewGuid().ToString("N")[..8]}";
        var user = await PostAsync("/api/v1/users", new
        {
            firstName,
            lastName,
            address = new
            {
                street = "5 rue Victor Hugo", postalCode = "33000", city = "Bordeaux"
            },
            monthlyExpenseQuota = Quota,
        });
        var report = await PostAsync("/api/v1/expense-reports", new
        {
            userId = user.GetProperty("id").GetString(), year = 2025, month = 10
        });
        return report.GetProperty("id").GetString().ShouldNotBeNull();
    }

    private Task<JsonElement> AddExpenseThroughTheApiAsync(string reportId, string date, string description)
    {
        return PostAsync($"/api/v1/expense-reports/{reportId}/expenses", new
        {
            date,
            description,
            amount = 10m,
            billingAddress = new
            {
                storeName = (string?)null, street = "1 rue de la Paix", postalCode = "75002", city = "Paris"
            },
        });
    }

    private async Task<JsonElement> PostAsync(string path, object body)
    {
        var response = await Page.APIRequest.PostAsync(path, new()
        {
            DataObject = body
        });
        response.Ok.ShouldBeTrue($"POST {path} answered {response.Status}");
        return (await response.JsonAsync()).ShouldNotBeNull();
    }
}
