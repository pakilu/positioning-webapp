using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.DAL.EF.Migrations
{
    /// <inheritdoc />
    public partial class AddFloorPlanToSessionConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "FloorPlanHeightMeters",
                table: "SessionConfigs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FloorPlanImagePath",
                table: "SessionConfigs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FloorPlanOpacity",
                table: "SessionConfigs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FloorPlanOriginXMeters",
                table: "SessionConfigs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FloorPlanOriginYMeters",
                table: "SessionConfigs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FloorPlanRotationDeg",
                table: "SessionConfigs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FloorPlanWidthMeters",
                table: "SessionConfigs",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FloorPlanHeightMeters",
                table: "SessionConfigs");

            migrationBuilder.DropColumn(
                name: "FloorPlanImagePath",
                table: "SessionConfigs");

            migrationBuilder.DropColumn(
                name: "FloorPlanOpacity",
                table: "SessionConfigs");

            migrationBuilder.DropColumn(
                name: "FloorPlanOriginXMeters",
                table: "SessionConfigs");

            migrationBuilder.DropColumn(
                name: "FloorPlanOriginYMeters",
                table: "SessionConfigs");

            migrationBuilder.DropColumn(
                name: "FloorPlanRotationDeg",
                table: "SessionConfigs");

            migrationBuilder.DropColumn(
                name: "FloorPlanWidthMeters",
                table: "SessionConfigs");
        }
    }
}
