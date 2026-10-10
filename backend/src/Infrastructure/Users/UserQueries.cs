using Application.Users.Queries;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Users;

internal sealed class UserQueries(AppDbContext dbContext) : IUserQueries
{
    public async Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Set<User>()
            .AsNoTracking()
            .Select(user => new UserSummary(user.Id, $"{user.FirstName} {user.LastName}"))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
