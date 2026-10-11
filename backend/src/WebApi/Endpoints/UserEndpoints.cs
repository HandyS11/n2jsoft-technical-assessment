using System.ComponentModel.DataAnnotations;
using Application.Exceptions;
using Application.Paging;
using Application.Users;
using Application.Users.Commands;
using Microsoft.AspNetCore.Http.HttpResults;
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
        users.MapPut("/{id:guid}", UpdateUserAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
        users.MapPost("/{id:guid}/activate", ActivateUserAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);
        users.MapPost("/{id:guid}/deactivate", DeactivateUserAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);
        users.MapDelete("/{id:guid}", DeleteUserAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);

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

    private static async Task<Created<UserDetails>> CreateUserAsync(
        SaveUserRequest request,
        CreateUserHandler handler,
        IUserStore userStore,
        CancellationToken cancellationToken)
    {
        var id = await handler.HandleAsync(
                new CreateUserCommand(
                    request.FirstName,
                    request.LastName,
                    request.Address.ToAddress(),
                    request.MonthlyExpenseQuota),
                cancellationToken)
            .ConfigureAwait(false);
        var user = await userStore.GetDetailsAsync(id, cancellationToken).ConfigureAwait(false)
                   ?? throw new InvalidOperationException($"User '{id}' cannot be read back after its creation.");
        return TypedResults.Created(new Uri($"/v1/users/{id}", UriKind.Relative), user);
    }

    private static async Task<NoContent> UpdateUserAsync(
        Guid id,
        SaveUserRequest request,
        UpdateUserHandler handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(
                new UpdateUserCommand(
                    id,
                    request.FirstName,
                    request.LastName,
                    request.Address.ToAddress(),
                    request.MonthlyExpenseQuota),
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
}
