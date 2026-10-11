using Application.Exceptions;
using Application.ExpenseReports;
using Application.ExpenseReports.Commands;
using Domain.Entities;
using Domain.ValueObjects;
using NSubstitute;

namespace UnitTests.ExpenseReports.Commands;

public sealed class DeleteExpenseReportHandlerTests
{
    private readonly IExpenseReportStore _expenseReportStore = Substitute.For<IExpenseReportStore>();

    [Fact]
    public async Task Removes_the_report_then_saves()
    {
        var user = User.Create("Juste", "Leblanc", new Address("12 rue de Rivoli", "75001", "Paris"), 10);
        var report = ExpenseReport.Create(user, new ReportMonth(2025, 10));
        _expenseReportStore.FindAsync(report.Id, Arg.Any<CancellationToken>()).Returns(report);

        await CreateHandler().HandleAsync(
            new DeleteExpenseReportCommand(report.Id),
            TestContext.Current.CancellationToken);

        Received.InOrder(() =>
        {
            _expenseReportStore.Remove(report);
            _ = _expenseReportStore.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Throws_not_found_for_an_unknown_report_without_saving()
    {
        await Should.ThrowAsync<NotFoundException>(() => CreateHandler().HandleAsync(
            new DeleteExpenseReportCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken));

        _expenseReportStore.DidNotReceive().Remove(Arg.Any<ExpenseReport>());
        await _expenseReportStore.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private DeleteExpenseReportHandler CreateHandler()
    {
        return new DeleteExpenseReportHandler(_expenseReportStore);
    }
}
