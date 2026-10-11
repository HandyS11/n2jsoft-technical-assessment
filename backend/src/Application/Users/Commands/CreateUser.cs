using Domain.Entities;
using Domain.ValueObjects;

namespace Application.Users.Commands;

public sealed record CreateUserCommand(string FirstName, string LastName, Address Address, int MonthlyExpenseQuota);

public sealed class CreateUserHandler(IUserStore userStore)
{
    /// <summary>The saved user, with the version the database gave it.</summary>
    public async Task<UserDetails> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var user = User.Create(command.FirstName, command.LastName, command.Address, command.MonthlyExpenseQuota);
        userStore.Add(user);
        await userStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return UserDetails.From(user);
    }
}
