using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AcceptanceTests.Support;
using Application.Expenses;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class ExpenseCommandSteps(
    ApiDriver api,
    KnownUsers knownUsers,
    KnownExpenseReports knownReports,
    KnownExpenses knownExpenses)
{
    /// <summary>The answers to requests sent at the same time, in no particular order.</summary>
    private readonly List<HttpResponseMessage> _concurrentResponses = [];

    private string? _entityTagRead;

    /// <summary>
    /// "Date" and "Description" are required; "Amount" (10.00), "Store" (none), "Street", "Postal code" and "City"
    /// default to valid values.
    /// </summary>
    [When("I add the following expense to {string}")]
    public Task WhenIAddTheFollowingExpenseToAsync(string title, DataTable table)
    {
        return AddAsync(knownReports.IdOf(title), ToRequest(table.Rows.ShouldHaveSingleItem()));
    }

    [When("I add an expense dated {string} to {string}")]
    public Task WhenIAddAnExpenseDatedToAsync(string date, string title)
    {
        return AddAsync(knownReports.IdOf(title), Request(date, $"Expense of {date}"));
    }

    /// <summary>
    /// Sends a valid October 2025 expense in which one field, given as a dotted JSON path ("billingAddress.city"), is
    /// replaced by a JSON value ("", null, 0.001).
    /// </summary>
    [When(@"^I add an expense to ""([^""]+)"" whose (\S+) is (.+)$")]
    public Task WhenIAddAnExpenseWhoseFieldIsAsync(string title, string field, string json)
    {
        return AddAsync(knownReports.IdOf(title), JsonRequests.With(Request("2025-10-15", "Taxi"), field, json));
    }

    [When("I add an expense to an unknown expense report")]
    public Task WhenIAddAnExpenseToAnUnknownExpenseReportAsync()
    {
        return AddAsync(Guid.NewGuid(), Request("2025-10-15", "Taxi"));
    }

    [When("two requests add an expense to {string} at the same time")]
    public async Task WhenTwoRequestsAddAnExpenseAtTheSameTimeAsync(string title)
    {
        var path = ExpensePaths.Of(knownReports.IdOf(title));
        using var first = TestEnvironment.Factory.CreateClient();
        using var second = TestEnvironment.Factory.CreateClient();
        var cancellationToken = TestContext.Current.CancellationToken;
        _concurrentResponses.AddRange(await Task.WhenAll(
            first.PostAsJsonAsync(new Uri(path, UriKind.Relative), Request("2025-10-20", "First"), cancellationToken),
            second.PostAsJsonAsync(new Uri(path, UriKind.Relative), Request("2025-10-21", "Second"),
                cancellationToken)));
    }

    /// <summary>
    /// Another connection deletes the report's user and holds its transaction open; the addition, sent meanwhile, must
    /// wait for the commit and then see the deletion, rather than check the user as they were before.
    /// </summary>
    [When("the user {string} is deleted while an expense is added to {string}")]
    public async Task WhenTheUserIsDeletedWhileAnExpenseIsAddedAsync(string name, string title)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = await TestEnvironment.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = """UPDATE users SET "DeletedAt" = now() WHERE "Id" = @id""";
            update.Parameters.AddWithValue("id", knownUsers.IdOf(name));
            await update.ExecuteNonQueryAsync(cancellationToken);
        }

        var addition = AddAsync(knownReports.IdOf(title), Request("2025-10-15", "Taxi"));
        await DatabaseLocks.WaitUntilAQueryWaitsForALockAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await addition;
    }

    [When("I update the expense {string} with")]
    public Task WhenIUpdateTheExpenseWithAsync(string description, DataTable table)
    {
        var (reportId, id) = knownExpenses.Of(description);
        return api.SendAsync(HttpMethod.Put, ExpensePaths.Of(reportId, id),
            ToRequest(table.Rows.ShouldHaveSingleItem()));
    }

    [When("I update the expense {string} to the date {string}")]
    public Task WhenIUpdateTheExpenseToTheDateAsync(string description, string date)
    {
        var (reportId, id) = knownExpenses.Of(description);
        return api.SendAsync(HttpMethod.Put, ExpensePaths.Of(reportId, id), Request(date, description));
    }

    [When(@"^I update the expense ""([^""]+)"" whose (\S+) is (.+)$")]
    public Task WhenIUpdateTheExpenseWhoseFieldIsAsync(string description, string field, string json)
    {
        var (reportId, id) = knownExpenses.Of(description);
        return api.SendAsync(HttpMethod.Put, ExpensePaths.Of(reportId, id),
            JsonRequests.With(Request("2025-10-15", description), field, json));
    }

    [When("I update an unknown expense of {string}")]
    public Task WhenIUpdateAnUnknownExpenseOfAsync(string title)
    {
        return api.SendAsync(HttpMethod.Put, ExpensePaths.Of(knownReports.IdOf(title), Guid.NewGuid()),
            Request("2025-10-15", "Taxi"));
    }

    [When("I read the expense {string}")]
    public async Task WhenIReadTheExpenseAsync(string description)
    {
        var (reportId, id) = knownExpenses.Of(description);
        await api.GetAsync(ExpensePaths.Of(reportId, id));
        _entityTagRead = api.Response.Headers.ETag.ShouldNotBeNull().ToString();
    }

    [When("I update the expense {string} with the ETag read")]
    public Task WhenIUpdateTheExpenseWithTheETagReadAsync(string description)
    {
        var (reportId, id) = knownExpenses.Of(description);
        return api.SendAsync(HttpMethod.Put, ExpensePaths.Of(reportId, id), Request("2025-10-16", description),
            _entityTagRead.ShouldNotBeNull());
    }

    [Then("the response locates the created expense")]
    public async Task ThenTheResponseLocatesTheCreatedExpenseAsync()
    {
        var expense = await api.ReadJsonAsync<ExpenseDetails>();
        var reportId = knownExpenses.Of(expense.Description).ReportId;
        api.Response.Headers.Location.ShouldNotBeNull().OriginalString
            .ShouldBe(ExpensePaths.Of(reportId, expense.Id));
    }

    [Then("one request is accepted and the other is refused with the code {string}")]
    public async Task ThenOneRequestIsAcceptedAndTheOtherIsRefusedAsync(string code)
    {
        _concurrentResponses.Select(response => (int)response.StatusCode).Order().ShouldBe([201, 422]);
        var refused = _concurrentResponses.Single(response => (int)response.StatusCode == 422);
        using var problem = JsonDocument.Parse(await refused.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("code").GetString().ShouldBe(code);
    }

    [AfterScenario]
    public void DisposeConcurrentResponses()
    {
        foreach (var response in _concurrentResponses)
        {
            response.Dispose();
        }
    }

    private static JsonObject Request(string date, string description)
    {
        return new JsonObject
        {
            ["date"] = date,
            ["description"] = description,
            ["amount"] = 10.00m,
            ["billingAddress"] = new JsonObject
            {
                ["storeName"] = null, ["street"] = "1 rue de la Paix", ["postalCode"] = "75002", ["city"] = "Paris",
            },
        };
    }

    private static JsonObject ToRequest(DataTableRow row)
    {
        return new JsonObject
        {
            ["date"] = row["Date"],
            ["description"] = row["Description"],
            ["amount"] = Amounts.Parse(Optional(row, "Amount", "10.00")),
            ["billingAddress"] = new JsonObject
            {
                ["storeName"] = row.TryGetValue("Store", out var store) ? store : null,
                ["street"] = Optional(row, "Street", "1 rue de la Paix"),
                ["postalCode"] = Optional(row, "Postal code", "75002"),
                ["city"] = Optional(row, "City", "Paris"),
            },
        };
    }

    private static string Optional(DataTableRow row, string column, string defaultValue)
    {
        return row.TryGetValue(column, out var value) ? value : defaultValue;
    }

    private async Task AddAsync(Guid reportId, JsonObject request)
    {
        await api.SendAsync(HttpMethod.Post, ExpensePaths.Of(reportId), request);
        if (api.Response.IsSuccessStatusCode)
        {
            var expense = await api.ReadJsonAsync<ExpenseDetails>();
            knownExpenses.Add(expense.Description, reportId, expense.Id);
        }
    }
}
