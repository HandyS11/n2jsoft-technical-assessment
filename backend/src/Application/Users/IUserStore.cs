using Application.Paging;

namespace Application.Users;

/// <summary>
/// The users feature's data access, implemented over the DbContext (which already is the unit of work). Deleted
/// users are invisible to every member.
/// </summary>
public interface IUserStore
{
    Task<PagedResult<UserSummary>> ListAsync(PageRequest request, CancellationToken cancellationToken);

    Task<UserDetails?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);
}
