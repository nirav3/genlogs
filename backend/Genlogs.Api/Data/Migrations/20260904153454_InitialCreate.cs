using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Genlogs.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Carriers",
                columns: table => new
                {
                    CarrierId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    UsdotNumber = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carriers", x => x.CarrierId);
                });

            migrationBuilder.CreateTable(
                name: "Lanes",
                columns: table => new
                {
                    LaneId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OriginCity = table.Column<string>(type: "TEXT", nullable: false),
                    DestinationCity = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefaultFallback = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lanes", x => x.LaneId);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    VehicleId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlateNumber = table.Column<string>(type: "TEXT", nullable: false),
                    CarrierId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.VehicleId);
                    table.ForeignKey(
                        name: "FK_Vehicles_Carriers_CarrierId",
                        column: x => x.CarrierId,
                        principalTable: "Carriers",
                        principalColumn: "CarrierId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DetectionEvents",
                columns: table => new
                {
                    DetectionId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LaneId = table.Column<int>(type: "INTEGER", nullable: false),
                    VehicleId = table.Column<int>(type: "INTEGER", nullable: false),
                    CapturedAt = table.Column<DateOnly>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetectionEvents", x => x.DetectionId);
                    table.ForeignKey(
                        name: "FK_DetectionEvents_Lanes_LaneId",
                        column: x => x.LaneId,
                        principalTable: "Lanes",
                        principalColumn: "LaneId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DetectionEvents_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DetectionEvents_LaneId",
                table: "DetectionEvents",
                column: "LaneId");

            migrationBuilder.CreateIndex(
                name: "IX_DetectionEvents_VehicleId",
                table: "DetectionEvents",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Lanes_OriginCity_DestinationCity",
                table: "Lanes",
                columns: new[] { "OriginCity", "DestinationCity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_CarrierId",
                table: "Vehicles",
                column: "CarrierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetectionEvents");

            migrationBuilder.DropTable(
                name: "Lanes");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "Carriers");
        }
    }
}
