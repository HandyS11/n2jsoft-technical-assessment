using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Application.Exceptions;
using Application.Paging;
using Application.Users;
using Application.Users.Commands;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using WebApi.Endpoints.Requests;

namespace WebApi.Endpoints;

internal static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var users = endpoints.NewVersionedApi("Users")
            .MapGroup("/v{version:apiVersion}/users")
            .HasApiVersion(1.0);

        users.MapGet("/", ListUsersAsync)
            .ProducesValidationProblem();
        users.MapGet("/{id:guid}", GetUserAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);
        users.MapPost("/", CreateUserAsync)
            .ProducesValidationProblem();
        // 409 on every change: another request saved the user while this one was changing it. 412 on PUT: the user
        // changed since the version given in If-Match.
        users.MapPut("/{id:guid}", UpdateUserAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed);
        users.MapPost("/{id:guid}/activate", ActivateUserAsync)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        users.MapPost("/{id:guid}/deactivate", DeactivateUserAsync)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        users.MapDelete("/{id:guid}", DeleteUserAsync)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }

    private static async Task<Ok<PagedResult<UserSummary>>> ListUsersAsync(
        IUserStore userStore,
        CancellationToken cancellationToken,
        [Range(1, PageRequest.MaxPage)] int page = 1,
        [Range(1, PageRequest.MaxPageSize)] int pageSize = PageRequest.DefaultPageSize,
        bool? isActive = null)
    {
        var users = await userStore.ListAsync(new PageRequest(page, pageSize), isActive, cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.Ok(users);
    }

    private static async Task<Ok<UserDetails>> GetUserAsync(
        Guid id,
        IUserStore userStore,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var user = await userStore.GetDetailsAsync(id, cancellationToken).ConfigureAwait(false)
                   ?? throw new NotFoundException("User", id);
        response.Headers.ETag = EntityTagOf(user);
        return TypedResults.Ok(user);
    }

    private static async Task<Created<UserDetails>> CreateUserAsync(
        SaveUserRequest request,
        CreateUserHandler handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var user = await handler.HandleAsync(
                new CreateUserCommand(
                    request.FirstName,
                    request.LastName,
                    request.Address.ToAddress(),
                    request.MonthlyExpenseQuota),
                cancellationToken)
            .ConfigureAwait(false);
        response.Headers.ETag = EntityTagOf(user);
        return TypedResults.Created(new Uri($"/v1/users/{user.Id}", UriKind.Relative), user);
    }

    /// <summary>
    /// A full replacement. With If-Match set to the user's version (its ETag), the update is refused with 412 when
    /// the user changed since it was read; without it, the update overwrites whatever is stored.
    /// </summary>
    private static async Task<NoContent> UpdateUserAsync(
        Guid id,
        SaveUserRequest request,
        UpdateUserHandler handler,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(
                new UpdateUserCommand(
                    id,
                    request.FirstName,
                    request.LastName,
                    request.Address.ToAddress(),
                    request.MonthlyExpenseQuota,
                    ExpectedVersionOf(ifMatch)),
                cancellationToken)
            .ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static Task<NoContent> ActivateUserAsync(
        Guid id,
        SetUserActivationHandler handler,
        CancellationToken cancellationToken)
    {
        return SetActivationAsync(new SetUserActivationCommand(id, IsActive: true), handler, cancellationToken);
    }

    private static Task<NoContent> DeactivateUserAsync(
        Guid id,
        SetUserActivationHandler handler,
        CancellationToken cancellationToken)
    {
        return SetActivationAsync(new SetUserActivationCommand(id, IsActive: false), handler, cancellationToken);
    }

    private static async Task<NoContent> SetActivationAsync(
        SetUserActivationCommand command,
        SetUserActivationHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(command, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DeleteUserAsync(
        Guid id,
        DeleteUserHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new DeleteUserCommand(id), cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static string EntityTagOf(UserDetails user)
    {
        return new EntityTagHeaderValue($"\"{user.Version}\"").ToString();
    }

    /// <summary>Null for no If-Match or "*" (any version); a 400 for a value that is not one of our ETags.</summary>
    private static uint? ExpectedVersionOf(string? ifMatch)
    {
        if (string.IsNullOrEmpty(ifMatch) || ifMatch == "*")
        {
            return null;
        }

        if (EntityTagHeaderValue.TryParse(ifMatch, out var entityTag)
            && !entityTag.IsWeak
            && uint.TryParse(entityTag.Tag.AsSpan(1, entityTag.Tag.Length - 2), CultureInfo.InvariantCulture,
                out var version))
        {
            return version;
        }

        throw new BadHttpRequestException("If-Match must be \"*\" or the ETag of the user.");
    }
}
