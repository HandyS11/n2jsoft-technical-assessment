using System.ComponentModel.DataAnnotations;
using Application.Exceptions;
using Application.Paging;
using Application.Users;
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
        users.MapGet("/{id:guid}", GetUserAsync);

        return endpoints;
    }

    private static async Task<Ok<PagedResult<UserSummary>>> ListUsersAsync(
        IUserStore userStore,
        CancellationToken cancellationToken,
        [Range(1, PageRequest.MaxPage)] int page = 1,
        [Range(1, PageRequest.MaxPageSize)] int pageSize = PageRequest.DefaultPageSize)
    {
        var users = await userStore.ListAsync(new PageRequest(page, pageSize), cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok(users);
    }

    private static async Task<Ok<UserDetails>> GetUserAsync(
        Guid id,
        IUserStore userStore,
        CancellationToken cancellationToken)
    {
        var user = await userStore.GetDetailsAsync(id, cancellationToken).ConfigureAwait(false)
                   ?? throw new NotFoundException("User", id);
        return TypedResults.Ok(user);
    }
}
