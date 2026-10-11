namespace Application.ExpenseReports;

/// <param name="ExpenseCount">The report's expenses, deleted ones left out.</param>
/// <param name="Total">The sum of their amounts, in euros.</param>
public sealed record ExpenseReportSummary(Guid Id, string Title, int Year, int Month, int ExpenseCount, decimal Total);
