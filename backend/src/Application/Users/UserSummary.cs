namespace Application.Users;

public sealed record UserSummary(Guid Id, string Name, string City, int MonthlyExpenseQuota, bool IsActive);
