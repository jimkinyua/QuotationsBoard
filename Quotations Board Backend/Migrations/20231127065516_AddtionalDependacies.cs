using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quotations_Board_Backend.Migrations
{
    public partial class AddtionalDependacies : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "BondId",
                table: "Quotations",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Quotations_BondId",
                table: "Quotations",
                column: "BondId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quotations_Bonds_BondId",
                table: "Quotations",
                column: "BondId",
                principalTable: "Bonds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Quotations_Bonds_BondId",
                table: "Quotations");

            migrationBuilder.DropIndex(
                name: "IX_Quotations_BondId",
                table: "Quotations");

            migrationBuilder.AlterColumn<string>(
                name: "BondId",
                table: "Quotations",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
