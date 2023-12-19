using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quotations_Board_Backend.Migrations
{
    public partial class RenameAfewColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TradedAt",
                table: "BondTradeLines");

            migrationBuilder.RenameColumn(
                name: "UploadedAt",
                table: "GorvermentBondTradeStages",
                newName: "TargetDate");

            migrationBuilder.AddColumn<string>(
                name: "BondId",
                table: "BondTradeLines",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BondId",
                table: "BondTradeLines");

            migrationBuilder.RenameColumn(
                name: "TargetDate",
                table: "GorvermentBondTradeStages",
                newName: "UploadedAt");

            migrationBuilder.AddColumn<DateTime>(
                name: "TradedAt",
                table: "BondTradeLines",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
