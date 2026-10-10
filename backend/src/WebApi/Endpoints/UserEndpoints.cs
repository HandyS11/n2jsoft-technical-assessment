using Application.Users.Queries;
using Microsoft.AspNetCore.Http.HttpResults;

namespace WebApi.Endpoints;

internal static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var users = endpoints.NewVersionedApi("Users")
            .MapGroup("/v{version:apiVersion}/users")
            .HasApiVersion(1.0);

        users.MapGet("/", ListUsersAsync);

        return endpoints;
    }

    private static async Task<Ok<IReadOnlyList<UserSummary>>> ListUsersAsync(
        IUserQueries userQueries,
        CancellationToken cancellationToken)
    {
        var users = await userQueries.ListAsync(cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(users);
    }
}
