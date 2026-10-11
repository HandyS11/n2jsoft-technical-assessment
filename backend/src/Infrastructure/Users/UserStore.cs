using Application.Exceptions;
using Application.Paging;
using Application.Users;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Users;

internal sealed class UserStore(AppDbContext dbContext) : IUserStore
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

    public void Add(User user)
    {
        dbContext.Set<User>().Add(user);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var entry = exception.Entries[0];
            throw new ConcurrencyConflictException(
                entry.Metadata.ClrType.Name,
                entry.Property("Id").CurrentValue!,
                exception);
        }
    }
}
