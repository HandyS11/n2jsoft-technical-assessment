using System.Text.Json.Nodes;
using AcceptanceTests.Support;
using Application.ExpenseReports;
using Application.Users;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace AcceptanceTests.StepDefinitions;

[Binding]
public sealed class ExpenseReportCommandSteps(ApiDriver api, KnownUsers knownUsers, KnownExpenseReports knownReports)
{
    private const string ExpenseReportsPath = "/v1/expense-reports";

    /// <summary>What the second of two concurrent requests threw when it saved; null if it did not throw.</summary>
    private Exception? _secondSaveFailure;

    [When("I create an expense report for {string} for {string}")]
    public Task WhenICreateAnExpenseReportForAsync(string name, string month)
    {
        var period = Months.Parse(month);
        return CreateAsync(Request(knownUsers.IdOf(name), period.Year, period.Month));
    }

    [When("I create an expense report for an unknown user")]
    public Task WhenICreateAnExpenseReportForAnUnknownUserAsync()
    {
        return CreateAsync(Request(Guid.NewGuid(), 2025, 10));
    }

    /// <summary>
    /// Sends a valid request in which one field is replaced by a JSON value (null, 1999). The user id is random:
    /// validation answers before any user is looked up.
    /// </summary>
    [When(@"^I create an expense report whose (\S+) is (.+)$")]
    public Task WhenICreateAnExpenseReportWhoseFieldIsAsync(string field, string json)
    {
        return CreateAsync(JsonRequests.With(Request(Guid.NewGuid(), 2025, 10), field, json));
    }

    /// <summary>
    /// Both requests read (the user, then whether the report exists) before either saves, as when they run at the
    /// same time; the HTTP pipeline cannot interleave them deterministically, so this drives the stores directly.
    /// </summary>
    [When("two requests create an expense report for {string} for {string} at the same time")]
    public async Task WhenTwoRequestsCreateAnExpenseReportAtTheSameTimeAsync(string name, string month)
    {
        var userId = knownUsers.IdOf(name);
        var period = Months.Parse(month);
        using var first = TestEnvironment.Factory.Services.CreateScope();
        using var second = TestEnvironment.Factory.Services.CreateScope();
        var firstStore = await StageCreationAsync(first, userId, period);
        var secondStore = await StageCreationAsync(second, userId, period);

        await firstStore.SaveChangesAsync(TestContext.Current.CancellationToken);
        _secondSaveFailure = await Record.ExceptionAsync(() =>
            secondStore.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Then("the response locates the created expense report")]
    public async Task ThenTheResponseLocatesTheCreatedExpenseReportAsync()
    {
        var report = await api.ReadJsonAsync<ExpenseReportDetails>();
        api.Response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"{ExpenseReportsPath}/{report.Id}");
    }

    [Then("the second request is refused as a duplicate")]
    public void ThenTheSecondRequestIsRefusedAsADuplicate()
    {
        _secondSaveFailure.ShouldBeOfType<DuplicateExpenseReportException>();
    }

    private static JsonObject Request(Guid userId, int year, int month)
    {
        return new JsonObject
        {
            ["userId"] = userId.ToString(), ["year"] = year, ["month"] = month,
        };
    }

    private static async Task<IExpenseReportStore> StageCreationAsync(
        IServiceScope scope,
        Guid userId,
        ReportMonth period)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var user = (await scope.ServiceProvider.GetRequiredService<IUserStore>().FindAsync(userId, cancellationToken))
            .ShouldNotBeNull();
        var expenseReportStore = scope.ServiceProvider.GetRequiredService<IExpenseReportStore>();
        (await expenseReportStore.ExistsAsync(userId, period, cancellationToken)).ShouldBeFalse();
        expenseReportStore.Add(ExpenseReport.Create(user, period));
        return expenseReportStore;
    }

    private async Task CreateAsync(JsonObject request)
    {
        await api.SendAsync(HttpMethod.Post, ExpenseReportsPath, request);
        if (api.Response.IsSuccessStatusCode)
        {
            var report = await api.ReadJsonAsync<ExpenseReportDetails>();
            knownReports.Add(report.Title, report.Id);
        }
    }
}
