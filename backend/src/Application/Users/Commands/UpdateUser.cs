using Domain.ValueObjects;

namespace Application.Users.Commands;

public sealed record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string LastName,
    Address Address,
    int MonthlyExpenseQuota);

public sealed class UpdateUserHandler(IUserStore userStore)
{
    public async Task HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await userStore.GetAsync(command.Id, cancellationToken).ConfigureAwait(false);
        user.UpdateDetails(command.FirstName, command.LastName, command.Address, command.MonthlyExpenseQuota);
        await userStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
