namespace Application.Expenses;

public sealed record ExpenseSummary(
    Guid Id,
    DateOnly Date,
    string Description,
    decimal Amount,
    ExpenseBillingAddress BillingAddress);
