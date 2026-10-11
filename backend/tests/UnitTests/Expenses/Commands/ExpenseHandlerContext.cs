using Application.ExpenseReports;
using Application.Expenses;
using Application.Users;
using Domain.Entities;
using Domain.ValueObjects;
using NSubstitute;
using UnitTests.Support;

namespace UnitTests.Expenses.Commands;

/// <summary>
/// What the expense handlers start from: Juste (quota 3) and his October 2025 report, both found through the locking
/// reads, stores that record what they are asked, and a transaction that runs at once.
/// </summary>
public sealed class ExpenseHandlerContext
{
    public static readonly BillingAddress Fnac = new("Fnac", new Address("12 rue de Rivoli", "75001", "Paris"));
    public static readonly DateOnly October15 = new(2025, 10, 15);

    public ExpenseHandlerContext()
    {
        User = User.Create("Juste", "Leblanc", new Address("12 rue de Rivoli", "75001", "Paris"), 3);
        Report = ExpenseReport.Create(User, new ReportMonth(2025, 10));
        ExpenseReportStore.FindLockedAsync(Report.Id, Arg.Any<CancellationToken>()).Returns(Report);
        UserStore.FindLockedAsync(User.Id, Arg.Any<CancellationToken>()).Returns(User);
    }

    public User User { get; }
    public ExpenseReport Report { get; }
    public IExpenseReportStore ExpenseReportStore { get; } = Substitute.For<IExpenseReportStore>();
    public IUserStore UserStore { get; } = Substitute.For<IUserStore>();
    public IExpenseStore ExpenseStore { get; } = Substitute.For<IExpenseStore>();
    public InlineTransactionRunner Transactions { get; } = new();

    /// <summary>An expense of the report, found by the expense store.</summary>
    public Expense AddStoredExpense()
    {
        var expense = Expense.Create(Report, User, 0, October15, "Taxi", 10m, Fnac);
        ExpenseStore.FindAsync(Report.Id, expense.Id, Arg.Any<CancellationToken>()).Returns(expense);
        return expense;
    }

    /// <summary>The report's user was deleted: the user's query filter hides them from the locking read.</summary>
    public void DeleteTheUser()
    {
        UserStore.FindLockedAsync(User.Id, Arg.Any<CancellationToken>()).Returns((User?)null);
    }

    public async Task ShouldNotHaveSavedAsync()
    {
        ExpenseStore.DidNotReceive().Add(Arg.Any<Expense>());
        await ExpenseStore.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
