using Application.Paging;
using Application.Users;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Users;

internal sealed class UserStore(AppDbContext dbContext) : IUserStore
{
    public async Task<PagedResult<UserSummary>> ListAsync(
        PageRequest request,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        var users = dbContext.Set<User>().AsNoTracking();
        if (isActive is { } active)
        {
            users = users.Where(user => user.IsActive == active);
        }

        var totalCount = await users.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await users
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            // Users sharing a name keep the same order from one page to the next.
            .ThenBy(user => user.Id)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(user => new UserSummary(
                user.Id,
                $"{user.FirstName} {user.LastName}",
                user.Address.City,
                user.MonthlyExpenseQuota,
                user.IsActive))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<UserSummary>(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<CursorPage<UserSummary>> ListActiveAsync(
        UserCursor? after,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var users = dbContext.Set<User>().AsNoTracking().Where(user => user.IsActive);
        if (after is not null)
        {
            // A row comparison: everything sorted after the last user read, whether that user still exists or not.
            users = users.Where(user => EF.Functions.GreaterThan(
                ValueTuple.Create(user.LastName, user.FirstName, user.Id),
                ValueTuple.Create(after.LastName, after.FirstName, after.Id)));
        }

        var rows = await users
            .OrderBy(user => user.LastName)
            .ThenBy(user => user.FirstName)
            .ThenBy(user => user.Id)
            // One more than asked tells whether there is a next page.
            .Take(pageSize + 1)
            .Select(user => new
            {
                user.LastName,
                user.FirstName,
                Summary = new UserSummary(
                    user.Id,
                    $"{user.FirstName} {user.LastName}",
                    user.Address.City,
                    user.MonthlyExpenseQuota,
                    user.IsActive),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var page = rows.Take(pageSize).ToList();
        var next = rows.Count > pageSize
            ? new UserCursor(page[^1].LastName, page[^1].FirstName, page[^1].Summary.Id).ToString()
            : null;
        return new CursorPage<UserSummary>(page.ConvertAll(row => row.Summary), next);
    }

    public Task<UserDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Set<User>()
            .AsNoTracking()
            .Where(user => user.Id == id)
            .Select(user => new UserDetails(
                user.Id,
                user.FirstName,
                user.LastName,
                new UserAddress(user.Address.Street, user.Address.PostalCode, user.Address.City),
                user.MonthlyExpenseQuota,
                user.IsActive,
                user.Version))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<User?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Set<User>().SingleOrDefaultAsync(user => user.Id == id, cancellationToken);
    }

    public Task<User?> FindLockedAsync(Guid id, CancellationToken cancellationToken)
    {
        // FOR SHARE: other readers and lockers go on, but an UPDATE of the row (deactivation, deletion) waits for the
        // commit. A row updated before is returned as committed. xmin is a system column, not part of "*".
        return dbContext.Set<User>()
            .FromSql($"""SELECT *, xmin FROM users WHERE "Id" = {id} FOR SHARE""")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public void Add(User user)
    {
        dbContext.Set<User>().Add(user);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
