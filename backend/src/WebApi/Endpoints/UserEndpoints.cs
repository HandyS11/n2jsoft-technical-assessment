using System.ComponentModel.DataAnnotations;
using Application.Paging;
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

    private static async Task<Ok<PagedResult<UserSummary>>> ListUsersAsync(
        IUserQueries userQueries,
        CancellationToken cancellationToken,
        [Range(1, PageRequest.MaxPage)] int page = 1,
        [Range(1, PageRequest.MaxPageSize)] int pageSize = PageRequest.DefaultPageSize)
    {
        var users = await userQueries.ListAsync(new PageRequest(page, pageSize), cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok(users);
    }
}
