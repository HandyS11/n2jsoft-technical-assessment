using Application.Exceptions;
using Application.Expenses.Commands;
using Domain.Exceptions;
using NSubstitute;

namespace UnitTests.Expenses.Commands;

public sealed class UpdateExpenseHandlerTests
{
    private readonly ExpenseHandlerContext _context = new();

    [Fact]
    public async Task Changes_the_expense_then_saves_within_one_transaction()
    {
        var expense = _context.AddStoredExpense();
        var savedInTransaction = false;
        _context.ExpenseStore.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                savedInTransaction = _context.Transactions.InTransaction;
                return Task.CompletedTask;
            });

        await CreateHandler().HandleAsync(Command(expense.Id), TestContext.Current.CancellationToken);

        expense.Description.ShouldBe("Hôtel");
        expense.Amount.ShouldBe(120m);
        savedInTransaction.ShouldBeTrue();
    }

    [Fact]
    public async Task Never_counts_the_expenses_against_the_quota()
    {
        var expense = _context.AddStoredExpense();
        _context.ExpenseStore.CountLiveAsync(_context.Report.Id, Arg.Any<CancellationToken>())
            .Returns(_context.User.MonthlyExpenseQuota);

        await CreateHandler().HandleAsync(Command(expense.Id), TestContext.Current.CancellationToken);

        await _context.ExpenseStore.DidNotReceive()
            .CountLiveAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _context.ExpenseStore.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Accepts_the_version_read()
    {
        var expense = _context.AddStoredExpense();

        await CreateHandler().HandleAsync(
            Command(expense.Id) with
            {
                ExpectedVersion = expense.Version
            },
            TestContext.Current.CancellationToken);

        await _context.ExpenseStore.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refuses_a_stale_version_without_saving()
    {
        var expense = _context.AddStoredExpense();

        await Should.ThrowAsync<ConcurrencyConflictException>(() => CreateHandler().HandleAsync(
            Command(expense.Id) with
            {
                ExpectedVersion = expense.Version + 1
            },
            TestContext.Current.CancellationToken));

        expense.Description.ShouldBe("Taxi");
        await _context.ShouldNotHaveSavedAsync();
    }

    [Fact]
    public async Task Refuses_a_date_outside_the_report_month_without_saving()
    {
        var expense = _context.AddStoredExpense();

        await Should.ThrowAsync<ExpenseDateOutsideReportMonthException>(() => CreateHandler().HandleAsync(
            Command(expense.Id) with
            {
                Date = new DateOnly(2025, 9, 30)
            },
            TestContext.Current.CancellationToken));

        await _context.ShouldNotHaveSavedAsync();
    }

    [Fact]
    public async Task Refuses_a_report_whose_user_is_deleted_without_saving()
    {
        var expense = _context.AddStoredExpense();
        _context.DeleteTheUser();

        await Should.ThrowAsync<DeletedUserException>(() =>
            CreateHandler().HandleAsync(Command(expense.Id), TestContext.Current.CancellationToken));

        expense.Description.ShouldBe("Taxi");
        await _context.ShouldNotHaveSavedAsync();
    }

    [Fact]
    public async Task Throws_not_found_for_an_unknown_expense_without_saving()
    {
        await Should.ThrowAsync<NotFoundException>(() =>
            CreateHandler().HandleAsync(Command(Guid.NewGuid()), TestContext.Current.CancellationToken));

        await _context.ShouldNotHaveSavedAsync();
    }

    [Fact]
    public async Task Throws_not_found_for_an_unknown_report_without_saving()
    {
        var expense = _context.AddStoredExpense();

        await Should.ThrowAsync<NotFoundException>(() => CreateHandler().HandleAsync(
            Command(expense.Id) with
            {
                ReportId = Guid.NewGuid()
            },
            TestContext.Current.CancellationToken));

        await _context.ShouldNotHaveSavedAsync();
    }

    private UpdateExpenseCommand Command(Guid id)
    {
        return new UpdateExpenseCommand(_context.Report.Id, id, new DateOnly(2025, 10, 2), "Hôtel", 120m,
            ExpenseHandlerContext.Fnac);
    }

    private UpdateExpenseHandler CreateHandler()
    {
        return new UpdateExpenseHandler(_context.Transactions, _context.ExpenseReportStore, _context.UserStore,
            _context.ExpenseStore);
    }
}
