using WebApi.Domain.Entities;
using WebApi.Infrastructure.Persistence;

namespace WebApi.Infrastructure.Api;

internal static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/users", (AppDbContext db) => Results.Ok(
            db.Set<User>().Select(u => new UserInfo(u.Id, $"{u.FirstName} {u.LastName}"))));

        return endpoints;
    }

    internal sealed record UserInfo(Guid Id, string Name);
}
