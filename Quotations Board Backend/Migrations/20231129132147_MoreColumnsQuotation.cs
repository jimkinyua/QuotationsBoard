using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quotations_Board_Backend.Migrations
{
    public partial class MoreColumnsQuotation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Volume",
                table: "Quotations",
                newName: "SellVolume");

            migrationBuilder.AddColumn<decimal>(
                name: "BuyVolume",
                table: "Quotations",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuyVolume",
                table: "Quotations");

            migrationBuilder.RenameColumn(
                name: "SellVolume",
                table: "Quotations",
                newName: "Volume");
        }
    }
}
