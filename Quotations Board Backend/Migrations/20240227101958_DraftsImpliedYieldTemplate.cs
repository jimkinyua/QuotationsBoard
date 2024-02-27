using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quotations_Board_Backend.Migrations
{
    public partial class DraftsImpliedYieldTemplate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DraftImpliedYields",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    YieldDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Yield = table.Column<double>(type: "float", nullable: false),
                    BondId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftImpliedYields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DraftImpliedYields_Bonds_BondId",
                        column: x => x.BondId,
                        principalTable: "Bonds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DraftImpliedYields_BondId",
                table: "DraftImpliedYields",
                column: "BondId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DraftImpliedYields");
        }
    }
}
