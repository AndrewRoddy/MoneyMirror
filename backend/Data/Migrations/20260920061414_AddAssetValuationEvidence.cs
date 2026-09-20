using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyMirror.backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetValuationEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetValuationEvidenceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetValuationRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceUsd = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    ListingTitle = table.Column<string>(type: "text", nullable: true),
                    Condition = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetValuationEvidenceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetValuationEvidenceRecords_AssetValuationRecords_AssetVa~",
                        column: x => x.AssetValuationRecordId,
                        principalTable: "AssetValuationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssetValuationEvidenceRecords_AssetValuationRecordId",
                table: "AssetValuationEvidenceRecords",
                column: "AssetValuationRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssetValuationEvidenceRecords");
        }
    }
}
