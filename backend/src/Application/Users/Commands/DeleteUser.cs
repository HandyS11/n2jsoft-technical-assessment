namespace Application.Users.Commands;

public sealed record DeleteUserCommand(Guid Id);

/// <summary>A logical deletion: the user stays stored but disappears from every query.</summary>
public sealed class DeleteUserHandler(IUserStore userStore, TimeProvider timeProvider)
{
    public async Task HandleAsync(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        var user = await userStore.GetAsync(command.Id, cancellationToken).ConfigureAwait(false);
        user.Delete(timeProvider.GetUtcNow());
        await userStore.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
