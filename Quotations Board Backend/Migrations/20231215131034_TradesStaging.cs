using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quotations_Board_Backend.Migrations
{
    public partial class TradesStaging : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GorvermentBondTradeStages",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GorvermentBondTradeStages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GorvermentBondTradeLinesStage",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GorvermentBondTradeStageId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Side = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecurityId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecutedSize = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExcecutedPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExecutionID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DirtyPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Yield = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TradeDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GorvermentBondTradeLinesStage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GorvermentBondTradeLinesStage_GorvermentBondTradeStages_GorvermentBondTradeStageId",
                        column: x => x.GorvermentBondTradeStageId,
                        principalTable: "GorvermentBondTradeStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GorvermentBondTradeLinesStage_GorvermentBondTradeStageId",
                table: "GorvermentBondTradeLinesStage",
                column: "GorvermentBondTradeStageId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GorvermentBondTradeLinesStage");

            migrationBuilder.DropTable(
                name: "GorvermentBondTradeStages");
        }
    }
}
