using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ERP.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PeriodLockingAndReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    ClosedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    MatchedJournalLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BankStatementLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchedJournalLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MatchedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MatchedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reconciliations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPeriods_Year_Month",
                table: "AccountingPeriods",
                columns: new[] { "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_MatchedJournalLineId",
                table: "BankStatementLines",
                column: "MatchedJournalLineId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_StatementDate",
                table: "BankStatementLines",
                column: "StatementDate");

            migrationBuilder.CreateIndex(
                name: "IX_Reconciliations_BankStatementLineId",
                table: "Reconciliations",
                column: "BankStatementLineId");

            migrationBuilder.CreateIndex(
                name: "IX_Reconciliations_MatchedJournalLineId",
                table: "Reconciliations",
                column: "MatchedJournalLineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountingPeriods");

            migrationBuilder.DropTable(
                name: "BankStatementLines");

            migrationBuilder.DropTable(
                name: "Reconciliations");
        }
    }
}
