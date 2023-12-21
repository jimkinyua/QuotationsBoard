using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quotations_Board_Backend.Migrations
{
    public partial class TBillAndYields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImpliedYields",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    YieldDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Yield = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BondId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpliedYields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImpliedYields_Bonds_BondId",
                        column: x => x.BondId,
                        principalTable: "Bonds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TBills",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IssueNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MaturityDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tenor = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TBills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TBillYields",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    YieldDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Yield = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TBillId = table.Column<string>(type: "nvarchar(450)", nullable: false)
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
                name: "IX_ImpliedYields_BondId",
                table: "ImpliedYields",
                column: "BondId");

            migrationBuilder.CreateIndex(
                name: "IX_TBillYields_TBillId",
                table: "TBillYields",
                column: "TBillId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImpliedYields");

            migrationBuilder.DropTable(
                name: "TBillYields");

            migrationBuilder.DropTable(
                name: "TBills");
        }
    }
}
