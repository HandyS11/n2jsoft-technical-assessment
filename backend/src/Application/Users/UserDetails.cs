namespace Application.Users;

public sealed record UserDetails(
    Guid Id,
    string FirstName,
    string LastName,
    UserAddress Address,
    int MonthlyExpenseQuota,
    bool IsActive);
