using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence;

internal sealed class ExpenseReportEntityTypeConfiguration : IEntityTypeConfiguration<ExpenseReport>
{
    /// <summary>The store recognises a duplicate report by the name of the index it violates.</summary>
    public const string UserYearAndMonthIndexName = "IX_expense_reports_UserId_Year_Month";

    public void Configure(EntityTypeBuilder<ExpenseReport> builder)
    {
        builder.ToTable("expense_reports");
        // Id is get-only, so EF Core does not discover it as the key by convention.
        builder.HasKey(report => report.Id);
        builder.Property(report => report.Title).HasMaxLength(ExpenseReport.TitleMaxLength);
        // No navigation: report reads never join the users, so the users' query filter cannot hide the report of a
        // deleted user. Restrict: users are only ever deleted logically.
        builder.HasOne<User>().WithMany().HasForeignKey(report => report.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(report => new
            {
                report.UserId, report.Year, report.Month
            })
            .IsUnique()
            .HasDatabaseName(UserYearAndMonthIndexName);
    }
}
