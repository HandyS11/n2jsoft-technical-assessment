using Application.Exceptions;
using Application.ExpenseReports;
using Application.ExpenseReports.Commands;
using Application.Persistence;
using Application.Users;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ValueObjects;
using NSubstitute;

namespace UnitTests.ExpenseReports.Commands;

public sealed class CreateExpenseReportHandlerTests
{
    private static readonly ReportMonth October2025 = new(2025, 10);
    private readonly IExpenseReportStore _expenseReportStore = Substitute.For<IExpenseReportStore>();

    private readonly User _juste =
        User.Create("Juste", "Leblanc", new Address("12 rue de Rivoli", "75001", "Paris"), 10);

    private readonly ITransactionRunner _transactions = Substitute.For<ITransactionRunner>();
    private readonly IUserStore _userStore = Substitute.For<IUserStore>();

    /// <summary>Whether the handler's work is running inside <see cref="ITransactionRunner.RunAsync{T}" />.</summary>
    private bool _inTransaction;

    public CreateExpenseReportHandlerTests()
    {
        _userStore.FindLockedAsync(_juste.Id, Arg.Any<CancellationToken>()).Returns(_juste);
        _transactions
            .RunAsync(Arg.Any<Func<CancellationToken, Task<ExpenseReportDetails>>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                _inTransaction = true;
                try
                {
                    return await call.Arg<Func<CancellationToken, Task<ExpenseReportDetails>>>()(
                        call.Arg<CancellationToken>());
                }
                finally
                {
                    _inTransaction = false;
                }
            });
    }

    [Fact]
    public async Task Adds_a_report_for_the_user_and_the_month_then_saves()
    {
        ExpenseReport? added = null;
        _expenseReportStore.Add(Arg.Do<ExpenseReport>(report => added = report));

        await CreateHandler().HandleAsync(
            new CreateExpenseReportCommand(_juste.Id, October2025),
            TestContext.Current.CancellationToken);

        added.ShouldNotBeNull();
        added.UserId.ShouldBe(_juste.Id);
        added.Period.ShouldBe(October2025);
        Received.InOrder(() =>
        {
            _expenseReportStore.Add(added);
            _ = _expenseReportStore.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Locks_the_user_and_saves_within_one_transaction()
    {
        var lockedInTransaction = false;
        var savedInTransaction = false;
        _userStore.FindLockedAsync(_juste.Id, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                lockedInTransaction = _inTransaction;
                return _juste;
            });
        _expenseReportStore.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                savedInTransaction = _inTransaction;
                return Task.CompletedTask;
            });

        await CreateHandler().HandleAsync(
            new CreateExpenseReportCommand(_juste.Id, October2025),
            TestContext.Current.CancellationToken);

        lockedInTransaction.ShouldBeTrue();
        savedInTransaction.ShouldBeTrue();
        await _userStore.DidNotReceive().FindAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Returns_the_details_of_the_new_report()
    {
        ExpenseReport? added = null;
        _expenseReportStore.Add(Arg.Do<ExpenseReport>(report => added = report));

        var details = await CreateHandler().HandleAsync(
            new CreateExpenseReportCommand(_juste.Id, October2025),
            TestContext.Current.CancellationToken);

        details.ShouldBe(new ExpenseReportDetails(
            added.ShouldNotBeNull().Id,
            "Juste Leblanc - Octobre 2025",
            _juste.Id,
            2025,
            10,
            0,
            0m,
            10,
            false));
    }

    [Fact]
    public async Task Throws_not_found_for_an_unknown_user_without_saving()
    {
        await Should.ThrowAsync<NotFoundException>(() => CreateHandler().HandleAsync(
            new CreateExpenseReportCommand(Guid.NewGuid(), October2025),
            TestContext.Current.CancellationToken));

        await ShouldNotHaveSavedAsync();
    }

    [Fact]
    public async Task Refuses_a_second_report_for_the_same_user_and_month_without_saving()
    {
        _expenseReportStore.ExistsAsync(_juste.Id, October2025, Arg.Any<CancellationToken>()).Returns(true);

        var exception = await Should.ThrowAsync<DuplicateExpenseReportException>(() => CreateHandler().HandleAsync(
            new CreateExpenseReportCommand(_juste.Id, October2025),
            TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("expense_report.already_exists");
        await ShouldNotHaveSavedAsync();
    }

    [Fact]
    public async Task Refuses_an_inactive_user_without_saving()
    {
        _juste.Deactivate();

        await Should.ThrowAsync<InactiveUserException>(() => CreateHandler().HandleAsync(
            new CreateExpenseReportCommand(_juste.Id, October2025),
            TestContext.Current.CancellationToken));

        await ShouldNotHaveSavedAsync();
    }

    private CreateExpenseReportHandler CreateHandler()
    {
        return new CreateExpenseReportHandler(_transactions, _userStore, _expenseReportStore);
    }

    private async Task ShouldNotHaveSavedAsync()
    {
        _expenseReportStore.DidNotReceive().Add(Arg.Any<ExpenseReport>());
        await _expenseReportStore.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
