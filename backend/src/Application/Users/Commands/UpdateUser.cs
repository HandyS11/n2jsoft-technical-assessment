using Application.Exceptions;
using Domain.ValueObjects;

namespace Application.Users.Commands;

/// <param name="ExpectedVersion">
/// The version the client read; when given, the update is rejected if the user changed since. Null overwrites
/// whatever is stored.
/// </param>
public sealed record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string LastName,
    Address Address,
    int MonthlyExpenseQuota,
    uint? ExpectedVersion = null);

public sealed class UpdateUserHandler(IUserStore userStore)
{
    public async Task HandleAsync(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var user = await userStore.GetAsync(command.Id, cancellationToken).ConfigureAwait(false);
        // A change saved between this read and the save below is caught by the store, through the row version.
        if (command.ExpectedVersion is { } expectedVersion && expectedVersion != user.Version)
        {
            throw new ConcurrencyConflictException("User", command.Id);
        }

        user.UpdateDetails(command.FirstName, command.LastName, command.Address, command.MonthlyExpenseQuota);
        await userStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
