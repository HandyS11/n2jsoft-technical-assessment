using Application.Paging;
using Application.Users.Queries;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Users;

internal sealed class UserQueries(AppDbContext dbContext) : IUserQueries
{
    public async Task<PagedResult<UserSummary>> ListAsync(PageRequest request, CancellationToken cancellationToken)
    {
        var users = dbContext.Set<User>().AsNoTracking();
        var totalCount = await users.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await users
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            // Users sharing a name keep the same order from one page to the next.
            .ThenBy(user => user.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(user => new UserSummary(user.Id, $"{user.FirstName} {user.LastName}"))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<UserSummary>(items, request.Page, request.PageSize, totalCount);
    }
}
