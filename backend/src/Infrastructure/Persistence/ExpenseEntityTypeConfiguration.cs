using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence;

internal sealed class ExpenseEntityTypeConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");
        // Id is get-only, so EF Core does not discover it as the key by convention.
        builder.HasKey(expense => expense.Id);
        builder.Property(expense => expense.Description).HasMaxLength(Expense.DescriptionMaxLength);
        // Up to 999 999,99: seven digits before the point, two after.
        builder.Property(expense => expense.Amount).HasPrecision(9, 2);
        builder.ComplexProperty(expense => expense.BillingAddress, billingAddress =>
        {
            billingAddress.Property(b => b.StoreName).HasMaxLength(BillingAddress.StoreNameMaxLength);
            billingAddress.ComplexProperty(b => b.Address, address =>
            {
                address.Property(a => a.Street).HasMaxLength(Address.StreetMaxLength);
                address.Property(a => a.PostalCode).HasMaxLength(Address.PostalCodeLength).IsFixedLength();
                address.Property(a => a.City).HasMaxLength(Address.CityMaxLength);
            });
        });
        // Npgsql maps a uint row version to the xmin system column, which Postgres changes on every update.
        builder.Property(expense => expense.Version).IsRowVersion();
        // A deleted expense is invisible to every query: it is neither listed, counted against the quota, nor changed.
        builder.HasQueryFilter(expense => expense.DeletedAt == null);
        // No navigation: the database deletes a report's expenses with it, deleted ones included, without EF Core
        // loading them.
        builder.HasOne<ExpenseReport>()
            .WithMany()
            .HasForeignKey(expense => expense.ExpenseReportId)
            .OnDelete(DeleteBehavior.Cascade);
        // The list (by date) and the count of a report's expenses.
        builder.HasIndex(expense => new
        {
            expense.ExpenseReportId, expense.Date
        });
    }
}
