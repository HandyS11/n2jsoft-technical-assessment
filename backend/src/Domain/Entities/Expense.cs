using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities;

/// <summary>An expense of an expense report, editable at any time and deleted logically.</summary>
public sealed class Expense
{
    public const int DescriptionMaxLength = 50;
    public const decimal MinAmount = 0.01m;
    public const decimal MaxAmount = 999_999.99m;

    /// <summary>Only <see cref="Create" /> and EF Core, which then sets the properties it loads, build expenses.</summary>
    private Expense()
    {
    }

    public Guid Id { get; } = Guid.CreateVersion7();
    public Guid ExpenseReportId { get; private set; }

    /// <summary>Always within the report's month.</summary>
    public DateOnly Date { get; private set; }

    public string Description { get; private set; } = string.Empty;

    /// <summary>In euros, with at most two decimals.</summary>
    public decimal Amount { get; private set; }

    public BillingAddress BillingAddress { get; private set; } = null!;

    /// <summary>Set by a logical deletion; a deleted expense is hidden from every query and frees a quota slot.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Set by the database on every save, so an update made from an older copy of the expense can be detected.</summary>
    public uint Version { get; }

    /// <param name="liveExpenseCount">The report's expenses not deleted, counted while the report is locked.</param>
    /// <exception cref="ExpenseDateOutsideReportMonthException">The date is outside the report's month.</exception>
    /// <exception cref="ExpenseQuotaExceededException">The report already holds the user's monthly quota.</exception>
    public static Expense Create(
        ExpenseReport report,
        User user,
        int liveExpenseCount,
        DateOnly date,
        string description,
        decimal amount,
        BillingAddress billingAddress)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id != report.UserId)
        {
            throw new ArgumentException("The user must be the report's.", nameof(user));
        }

        var trimmedDescription = CheckDetails(report, date, description, amount, billingAddress);
        if (liveExpenseCount >= user.MonthlyExpenseQuota)
        {
            throw new ExpenseQuotaExceededException(user.Id, user.MonthlyExpenseQuota);
        }

        var expense = new Expense
        {
            ExpenseReportId = report.Id
        };
        expense.Apply(date, trimmedDescription, amount, billingAddress);
        return expense;
    }

    /// <summary>Checks every detail before changing any, so a rejected update leaves the expense as it was.</summary>
    /// <exception cref="ExpenseDateOutsideReportMonthException">The date is outside the report's month.</exception>
    public void Update(
        ExpenseReport report,
        DateOnly date,
        string description,
        decimal amount,
        BillingAddress billingAddress)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Id != ExpenseReportId)
        {
            throw new ArgumentException("The report must be the expense's.", nameof(report));
        }

        Apply(date, CheckDetails(report, date, description, amount, billingAddress), amount, billingAddress);
    }

    public void Delete(DateTimeOffset now)
    {
        DeletedAt ??= now;
    }

    /// <summary>The values first (400), then the report-month rule (422). Returns the trimmed description.</summary>
    private static string CheckDetails(
        ExpenseReport report,
        DateOnly date,
        string description,
        decimal amount,
        BillingAddress billingAddress)
    {
        ArgumentNullException.ThrowIfNull(billingAddress);
        var trimmedDescription = Text.Required(description, DescriptionMaxLength);
        if (amount is < MinAmount or > MaxAmount)
        {
            throw new InvalidValueException(nameof(amount),
                $"The amount must be between {MinAmount} and {MaxAmount}.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new InvalidValueException(nameof(amount), "The amount must have at most two decimals.");
        }

        if (!report.Period.Contains(date))
        {
            throw new ExpenseDateOutsideReportMonthException(date, report.Period);
        }

        return trimmedDescription;
    }

    private void Apply(DateOnly date, string description, decimal amount, BillingAddress billingAddress)
    {
        Date = date;
        Description = description;
        Amount = amount;
        BillingAddress = billingAddress;
    }
}
