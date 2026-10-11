using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Expenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    BillingAddress_StoreName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BillingAddress_Address_City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BillingAddress_Address_PostalCode = table.Column<string>(type: "character(5)", fixedLength: true, maxLength: 5, nullable: false),
                    BillingAddress_Address_Street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_expenses_expense_reports_ExpenseReportId",
                        column: x => x.ExpenseReportId,
                        principalTable: "expense_reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_expenses_ExpenseReportId_Date",
                table: "expenses",
                columns: new[] { "ExpenseReportId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "expenses");
        }
    }
}
