using Domain.Entities;

namespace Application.Users;

/// <summary><see cref="Version" /> is sent back to update the user only if nobody changed it in between.</summary>
public sealed record UserDetails(
    Guid Id,
    string FirstName,
    string LastName,
    UserAddress Address,
    int MonthlyExpenseQuota,
    bool IsActive,
    uint Version)
{
    public static UserDetails From(User user)
    {
        return new UserDetails(
            user.Id,
            user.FirstName,
            user.LastName,
            new UserAddress(user.Address.Street, user.Address.PostalCode, user.Address.City),
            user.MonthlyExpenseQuota,
            user.IsActive,
            user.Version);
    }
}
