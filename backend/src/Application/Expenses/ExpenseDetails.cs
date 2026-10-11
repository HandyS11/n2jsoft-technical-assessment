using Domain.Entities;

namespace Application.Expenses;

/// <summary><see cref="Version" /> is sent back to update the expense only if nobody changed it in between.</summary>
public sealed record ExpenseDetails(
    Guid Id,
    DateOnly Date,
    string Description,
    decimal Amount,
    ExpenseBillingAddress BillingAddress,
    uint Version)
{
    public static ExpenseDetails From(Expense expense)
    {
        return new ExpenseDetails(
            expense.Id,
            expense.Date,
            expense.Description,
            expense.Amount,
            new ExpenseBillingAddress(
                expense.BillingAddress.StoreName,
                expense.BillingAddress.Address.Street,
                expense.BillingAddress.Address.PostalCode,
                expense.BillingAddress.Address.City),
            expense.Version);
    }
}
