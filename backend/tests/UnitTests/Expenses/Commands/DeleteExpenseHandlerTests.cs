using Application.Exceptions;
using Application.Expenses.Commands;
using Domain.Exceptions;
using NSubstitute;

namespace UnitTests.Expenses.Commands;

public sealed class DeleteExpenseHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 9, 30, 0, TimeSpan.Zero);
    private readonly ExpenseHandlerContext _context = new();
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();

    public DeleteExpenseHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
    }

    [Fact]
    public async Task Marks_the_expense_as_deleted_now_then_saves_within_one_transaction()
    {
        var expense = _context.AddStoredExpense();
        var savedInTransaction = false;
        _context.ExpenseStore.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                savedInTransaction = _context.Transactions.InTransaction;
                return Task.CompletedTask;
            });

        await CreateHandler().HandleAsync(
            new DeleteExpenseCommand(_context.Report.Id, expense.Id),
            TestContext.Current.CancellationToken);

        expense.DeletedAt.ShouldBe(Now);
        savedInTransaction.ShouldBeTrue();
    }

    [Fact]
    public async Task Refuses_a_report_whose_user_is_deleted_without_saving()
    {
        var expense = _context.AddStoredExpense();
        _context.DeleteTheUser();

        await Should.ThrowAsync<DeletedUserException>(() => CreateHandler().HandleAsync(
            new DeleteExpenseCommand(_context.Report.Id, expense.Id),
            TestContext.Current.CancellationToken));

        expense.DeletedAt.ShouldBeNull();
        await _context.ShouldNotHaveSavedAsync();
    }

    [Fact]
    public async Task Throws_not_found_for_an_unknown_expense_without_saving()
    {
        await Should.ThrowAsync<NotFoundException>(() => CreateHandler().HandleAsync(
            new DeleteExpenseCommand(_context.Report.Id, Guid.NewGuid()),
            TestContext.Current.CancellationToken));

        await _context.ShouldNotHaveSavedAsync();
    }

    [Fact]
    public async Task Throws_not_found_for_an_unknown_report_without_saving()
    {
        await Should.ThrowAsync<NotFoundException>(() => CreateHandler().HandleAsync(
            new DeleteExpenseCommand(Guid.NewGuid(), Guid.NewGuid()),
            TestContext.Current.CancellationToken));

        await _context.ShouldNotHaveSavedAsync();
    }

    private DeleteExpenseHandler CreateHandler()
    {
        return new DeleteExpenseHandler(_context.Transactions, _context.ExpenseReportStore, _context.UserStore,
            _context.ExpenseStore, _timeProvider);
    }
}
