using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new UserEntityTypeConfiguration());
    }

    internal static async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
    {
        if (!await context.Set<User>().AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            await context.Set<User>().AddRangeAsync(
                [
                    new User
                    {
                        FirstName = "Juste", LastName = "Leblanc"
                    },
                    new User
                    {
                        FirstName = "Marc", LastName = "Assin"
                    },
                ],
                cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
