using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoneyMirror.backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalAssetIdentificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdentifiedProductModel",
                table: "PhysicalAssets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageReference",
                table: "PhysicalAssets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "PhysicalAssets",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdentifiedProductModel",
                table: "PhysicalAssets");

            migrationBuilder.DropColumn(
                name: "ImageReference",
                table: "PhysicalAssets");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "PhysicalAssets");
        }
    }
}
