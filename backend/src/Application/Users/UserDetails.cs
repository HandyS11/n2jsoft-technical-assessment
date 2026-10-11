using Domain.Entities;

namespace Application.Users;

public sealed record UserDetails(
    Guid Id,
    string FirstName,
    string LastName,
    UserAddress Address,
    int MonthlyExpenseQuota,
    bool IsActive)
{
    public static UserDetails From(User user)
    {
        return new UserDetails(
            user.Id,
            user.FirstName,
            user.LastName,
            new UserAddress(user.Address.Street, user.Address.PostalCode, user.Address.City),
            user.MonthlyExpenseQuota,
            user.IsActive);
    }
}
