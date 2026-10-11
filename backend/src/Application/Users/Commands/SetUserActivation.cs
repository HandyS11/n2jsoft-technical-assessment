namespace Application.Users.Commands;

public sealed record SetUserActivationCommand(Guid Id, bool IsActive);

/// <summary>Asking for the status the user already has changes nothing and is not an error.</summary>
public sealed class SetUserActivationHandler(IUserStore userStore)
{
    public async Task HandleAsync(SetUserActivationCommand command, CancellationToken cancellationToken)
    {
        var user = await userStore.GetAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (command.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await userStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
