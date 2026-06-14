using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.DAL.EF.Migrations
{
    /// <inheritdoc />
    public partial class SyncCurrentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PositionResults_Chips_TagChipId",
                table: "PositionResults");

            migrationBuilder.AddForeignKey(
                name: "FK_PositionResults_Chips_TagChipId",
                table: "PositionResults",
                column: "TagChipId",
                principalTable: "Chips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PositionResults_Chips_TagChipId",
                table: "PositionResults");

            migrationBuilder.AddForeignKey(
                name: "FK_PositionResults_Chips_TagChipId",
                table: "PositionResults",
                column: "TagChipId",
                principalTable: "Chips",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
