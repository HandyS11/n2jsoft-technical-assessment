using Domain.Entities;
using Domain.ValueObjects;
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
                    User.Create("Juste", "Leblanc", new Address("12 rue de Rivoli", "75001", "Paris"), 10),
                    User.Create("Marc", "Assin", new Address("3 place Bellecour", "69002", "Lyon"), 10),
                ],
                cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
