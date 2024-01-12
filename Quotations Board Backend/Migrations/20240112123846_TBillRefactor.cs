using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quotations_Board_Backend.Migrations
{
    public partial class TBillRefactor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TBillYields");

            migrationBuilder.AddColumn<decimal>(
                name: "Yield",
                table: "TBills",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Yield",
                table: "TBills");

            migrationBuilder.CreateTable(
                name: "TBillYields",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TBillId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Yield = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    YieldDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBillYields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TBillYields_TBills_TBillId",
                        column: x => x.TBillId,
                        principalTable: "TBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TBillYields_TBillId",
                table: "TBillYields",
                column: "TBillId");
        }
    }
}
