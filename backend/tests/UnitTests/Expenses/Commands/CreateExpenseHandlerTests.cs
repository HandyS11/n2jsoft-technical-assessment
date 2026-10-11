using Application.Exceptions;
using Application.Expenses;
using Application.Expenses.Commands;
using Domain.Entities;
using Domain.Exceptions;
using NSubstitute;

namespace UnitTests.Expenses.Commands;

public sealed class CreateExpenseHandlerTests
{
    private readonly ExpenseHandlerContext _context = new();

    [Fact]
    public async Task Adds_the_expense_to_the_report_then_saves()
    {
        Expense? added = null;
        _context.ExpenseStore.Add(Arg.Do<Expense>(expense => added = expense));

        var details = await CreateHandler().HandleAsync(Command(), TestContext.Current.CancellationToken);

        added.ShouldNotBeNull();
        added.ExpenseReportId.ShouldBe(_context.Report.Id);
        added.Description.ShouldBe("Taxi");
        details.ShouldBe(ExpenseDetails.From(added));
        Received.InOrder(() =>
        {
            _context.ExpenseStore.Add(added);
            _ = _context.ExpenseStore.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Locks_the_report_then_the_user_and_saves_within_one_transaction()
    {
        var lockedInTransaction = false;
        var savedInTransaction = false;
        _context.ExpenseReportStore.FindLockedAsync(_context.Report.Id, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                lockedInTransaction = _context.Transactions.InTransaction;
                return _context.Report;
            });
        _context.ExpenseStore.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                savedInTransaction = _context.Transactions.InTransaction;
                return Task.CompletedTask;
            });

        await CreateHandler().HandleAsync(Command(), TestContext.Current.CancellationToken);

        lockedInTransaction.ShouldBeTrue();
        savedInTransaction.ShouldBeTrue();
        Received.InOrder(() =>
        {
            _ = _context.ExpenseReportStore.FindLockedAsync(_context.Report.Id, Arg.Any<CancellationToken>());
            _ = _context.UserStore.FindLockedAsync(_context.User.Id, Arg.Any<CancellationToken>());
            _ = _context.ExpenseStore.CountLiveAsync(_context.Report.Id, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Refuses_an_expense_once_the_quota_is_reached_without_saving()
    {
        _context.ExpenseStore.CountLiveAsync(_context.Report.Id, Arg.Any<CancellationToken>())
            .Returns(_context.User.MonthlyExpenseQuota);

        await Should.ThrowAsync<ExpenseQuotaExceededException>(() =>
            CreateHandler().HandleAsync(Command(), TestContext.Current.CancellationToken));

        await _context.ShouldNotHaveSavedAsync();
    }

    [Fact]
    public async Task Refuses_a_date_outside_the_report_month_without_saving()
    {
        await Should.ThrowAsync<ExpenseDateOutsideReportMonthException>(() => CreateHandler().HandleAsync(
            Command() with
            {
                Date = new DateOnly(2025, 11, 1)
            },
            TestContext.Current.CancellationToken));

        await _context.ShouldNotHaveSavedAsync();
    }

    [Fact]
    public async Task Refuses_a_report_whose_user_is_deleted_without_saving()
    {
        _context.DeleteTheUser();

        var exception = await Should.ThrowAsync<DeletedUserException>(() =>
            CreateHandler().HandleAsync(Command(), TestContext.Current.CancellationToken));

        exception.Code.ShouldBe("expense_report.deleted_user");
        await _context.ShouldNotHaveSavedAsync();
    }

    [Fact]
    public async Task Throws_not_found_for_an_unknown_report_without_saving()
    {
        await Should.ThrowAsync<NotFoundException>(() => CreateHandler().HandleAsync(
            Command() with
            {
                ReportId = Guid.NewGuid()
            },
            TestContext.Current.CancellationToken));

        await _context.ShouldNotHaveSavedAsync();
    }

    private CreateExpenseCommand Command()
    {
        return new CreateExpenseCommand(_context.Report.Id, ExpenseHandlerContext.October15, "Taxi", 10m,
            ExpenseHandlerContext.Fnac);
    }

    private CreateExpenseHandler CreateHandler()
    {
        return new CreateExpenseHandler(_context.Transactions, _context.ExpenseReportStore, _context.UserStore,
            _context.ExpenseStore);
    }
}
