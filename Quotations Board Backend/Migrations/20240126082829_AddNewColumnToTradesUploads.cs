using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quotations_Board_Backend.Migrations
{
    public partial class AddNewColumnToTradesUploads : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TransactionID",
                table: "GorvermentBondTradeLinesStage",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionID",
                table: "BondTradeLines",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TransactionID",
                table: "GorvermentBondTradeLinesStage");

            migrationBuilder.DropColumn(
                name: "TransactionID",
                table: "BondTradeLines");
        }
    }
}
