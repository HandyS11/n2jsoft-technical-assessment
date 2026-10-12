using Application.Exceptions;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence;

internal sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Every save goes through here, whichever store asks for it, so a database error means the same thing to the
    /// client whatever the handler saved through.
    /// </summary>
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var entry = exception.Entries[0];
            var resource = entry.Metadata.ClrType.Name;
            var key = entry.Property("Id").CurrentValue!;
            // Reports are never updated: a report missing on save was deleted by another request since it was read.
            // A user or an expense is checked through its row version: another request saved it since it was read.
            throw entry.Entity is ExpenseReport
                ? new NotFoundException(resource, key, exception)
                : new ConcurrencyConflictException(resource, key, exception);
        }
        catch (DbUpdateException exception) when (IsDuplicateReport(exception))
        {
            // Another request created the same report between the check for one and this save.
            throw new DuplicateExpenseReportException(exception);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder
            .ApplyConfiguration(new UserEntityTypeConfiguration())
            .ApplyConfiguration(new ExpenseReportEntityTypeConfiguration())
            .ApplyConfiguration(new ExpenseEntityTypeConfiguration());
    }

    internal static async Task SeedAsync(DbContext context, CancellationToken cancellationToken)
    {
        // Soft-deleted users count: otherwise the demo data would be inserted again once all the users are deleted.
        if (!await context.Set<User>().IgnoreQueryFilters().AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            var demoData = DemoData.Create(DateOnly.FromDateTime(TimeProvider.System.GetLocalNow().DateTime));
            await context.Set<User>().AddRangeAsync(demoData.Users, cancellationToken).ConfigureAwait(false);
            await context.Set<ExpenseReport>().AddRangeAsync(demoData.ExpenseReports, cancellationToken)
                .ConfigureAwait(false);
            await context.Set<Expense>().AddRangeAsync(demoData.Expenses, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsDuplicateReport(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: ExpenseReportEntityTypeConfiguration.UserYearAndMonthIndexName,
        };
    }
}
