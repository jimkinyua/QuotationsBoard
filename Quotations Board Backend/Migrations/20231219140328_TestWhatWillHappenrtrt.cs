using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quotations_Board_Backend.Migrations
{
    public partial class TestWhatWillHappenrtrt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BondTrades_Bonds_BondId",
                table: "BondTrades");

            migrationBuilder.DropIndex(
                name: "IX_BondTrades_BondId",
                table: "BondTrades");

            migrationBuilder.DropColumn(
                name: "BondId",
                table: "BondTrades");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BondId",
                table: "BondTrades",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_BondTrades_BondId",
                table: "BondTrades",
                column: "BondId");

            migrationBuilder.AddForeignKey(
                name: "FK_BondTrades_Bonds_BondId",
                table: "BondTrades",
                column: "BondId",
                principalTable: "Bonds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
