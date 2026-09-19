using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyMirror.backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialRecordAsOfDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AsOfDate",
                table: "Liabilities",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AsOfDate",
                table: "FinancialAccounts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AsOfDate",
                table: "Liabilities");

            migrationBuilder.DropColumn(
                name: "AsOfDate",
                table: "FinancialAccounts");
        }
    }
}
