namespace Application.Users.Queries;

public interface IUserQueries
{
    Task<IReadOnlyList<UserSummary>> ListAsync(CancellationToken cancellationToken);
}
