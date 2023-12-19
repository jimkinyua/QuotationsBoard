using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quotations_Board_Backend.Migrations
{
    public partial class AddBondTrades : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BondTrades",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BondId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TradeDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BondTrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BondTrades_Bonds_BondId",
                        column: x => x.BondId,
                        principalTable: "Bonds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BondTradeLines",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BondTradeId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Side = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SecurityId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecutedSize = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExcecutedPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ExecutionID = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DirtyPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Yield = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TradedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BondTradeLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BondTradeLines_BondTrades_BondTradeId",
                        column: x => x.BondTradeId,
                        principalTable: "BondTrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BondTradeLines_BondTradeId",
                table: "BondTradeLines",
                column: "BondTradeId");

            migrationBuilder.CreateIndex(
                name: "IX_BondTrades_BondId",
                table: "BondTrades",
                column: "BondId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BondTradeLines");

            migrationBuilder.DropTable(
                name: "BondTrades");
        }
    }
}
