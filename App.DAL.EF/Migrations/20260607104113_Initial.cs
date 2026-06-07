using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.DAL.EF.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Chips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Chips", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SessionConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PlannedDurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SessionConfigChips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    XCoord = table.Column<decimal>(type: "numeric", nullable: true),
                    YCoord = table.Column<decimal>(type: "numeric", nullable: true),
                    ZCoord = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionConfigChips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionConfigChips_Chips_ChipId",
                        column: x => x.ChipId,
                        principalTable: "Chips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionConfigChips_SessionConfigs_SessionConfigId",
                        column: x => x.SessionConfigId,
                        principalTable: "SessionConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionConfigId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_SessionConfigs_SessionConfigId",
                        column: x => x.SessionConfigId,
                        principalTable: "SessionConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PositionResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagChipId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    XCoord = table.Column<decimal>(type: "numeric", nullable: false),
                    YCoord = table.Column<decimal>(type: "numeric", nullable: false),
                    ZCoord = table.Column<decimal>(type: "numeric", nullable: true),
                    Accuracy = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionResults_Chips_TagChipId",
                        column: x => x.TagChipId,
                        principalTable: "Chips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PositionResults_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RawMeasurements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TagChipId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnchorChipId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Distance = table.Column<decimal>(type: "numeric", nullable: true),
                    Rssi = table.Column<decimal>(type: "numeric", nullable: true),
                    Snr = table.Column<decimal>(type: "numeric", nullable: true),
                    Quality = table.Column<decimal>(type: "numeric", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RawMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RawMeasurements_Chips_AnchorChipId",
                        column: x => x.AnchorChipId,
                        principalTable: "Chips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawMeasurements_Chips_TagChipId",
                        column: x => x.TagChipId,
                        principalTable: "Chips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RawMeasurements_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chips_DeviceIdentifier",
                table: "Chips",
                column: "DeviceIdentifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PositionResults_SessionId",
                table: "PositionResults",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionResults_TagChipId",
                table: "PositionResults",
                column: "TagChipId");

            migrationBuilder.CreateIndex(
                name: "IX_RawMeasurements_AnchorChipId",
                table: "RawMeasurements",
                column: "AnchorChipId");

            migrationBuilder.CreateIndex(
                name: "IX_RawMeasurements_SessionId",
                table: "RawMeasurements",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RawMeasurements_TagChipId",
                table: "RawMeasurements",
                column: "TagChipId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionConfigChips_ChipId",
                table: "SessionConfigChips",
                column: "ChipId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionConfigChips_SessionConfigId_ChipId",
                table: "SessionConfigChips",
                columns: new[] { "SessionConfigId", "ChipId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_SessionConfigId",
                table: "Sessions",
                column: "SessionConfigId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PositionResults");

            migrationBuilder.DropTable(
                name: "RawMeasurements");

            migrationBuilder.DropTable(
                name: "SessionConfigChips");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Chips");

            migrationBuilder.DropTable(
                name: "SessionConfigs");
        }
    }
}
