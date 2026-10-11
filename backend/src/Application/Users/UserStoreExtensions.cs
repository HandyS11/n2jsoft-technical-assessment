using Application.Exceptions;
using Domain.Entities;

namespace Application.Users;

internal static class UserStoreExtensions
{
    /// <summary>The tracked user; throws <see cref="NotFoundException" /> (404) when it is unknown or deleted.</summary>
    public static async Task<User> GetAsync(this IUserStore userStore, Guid id, CancellationToken cancellationToken)
    {
        return await userStore.FindAsync(id, cancellationToken).ConfigureAwait(false)
               ?? throw new NotFoundException("User", id);
    }
}
