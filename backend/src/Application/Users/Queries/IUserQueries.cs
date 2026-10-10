using Application.Paging;

namespace Application.Users.Queries;

public interface IUserQueries
{
    Task<PagedResult<UserSummary>> ListAsync(PageRequest request, CancellationToken cancellationToken);
}
