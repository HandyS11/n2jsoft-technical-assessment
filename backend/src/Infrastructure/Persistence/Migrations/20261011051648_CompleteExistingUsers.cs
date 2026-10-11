using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteExistingUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // UserDetails added the address, status and quota columns with defaults the domain rejects (blank
            // address, quota 0), and the seed skips a database that already has users. Users stored before it get
            // a placeholder address to correct, the active status User.Create gives, and the seed's quota.
            migrationBuilder.Sql(
                """
                UPDATE users
                SET "Address_Street" = 'Adresse à compléter', "Address_PostalCode" = '00000', "Address_City" = 'À compléter'
                WHERE "Address_Street" = '';

                UPDATE users
                SET "IsActive" = TRUE, "MonthlyExpenseQuota" = 10
                WHERE "MonthlyExpenseQuota" = 0;
                """);

            // The model now maps User.Version to the xmin system column: Postgres maintains it, no column to add.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The backfilled values are valid data: nothing to undo.
        }
    }
}
