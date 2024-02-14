using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quotations_Board_Backend.Migrations
{
    public partial class BenchMarkBond : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBenchMarkBond",
                table: "Bonds",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBenchMarkBond",
                table: "Bonds");
        }
    }
}
