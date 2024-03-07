using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quotations_Board_Backend.Migrations
{
    public partial class TBillImpliedYields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TBillImpliedYields",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Yield = table.Column<double>(type: "float", nullable: false),
                    Tenor = table.Column<double>(type: "float", nullable: false),
                    TBillId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBillImpliedYields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TBillImpliedYields_TBills_TBillId",
                        column: x => x.TBillId,
                        principalTable: "TBills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TBillImpliedYields_TBillId",
                table: "TBillImpliedYields",
                column: "TBillId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TBillImpliedYields");
        }
    }
}
