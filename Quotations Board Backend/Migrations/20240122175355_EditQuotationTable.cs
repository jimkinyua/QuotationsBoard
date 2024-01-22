using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Quotations_Board_Backend.Migrations
{
    public partial class EditQuotationTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Quotations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "QuotationEdits",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BuyingYield = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SellingYield = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BuyVolume = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SellVolume = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InstitutionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BondId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QuotationId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuotationEdits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuotationEdits_Quotations_QuotationId",
                        column: x => x.QuotationId,
                        principalTable: "Quotations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuotationEdits_QuotationId",
                table: "QuotationEdits",
                column: "QuotationId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuotationEdits");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Quotations");
        }
    }
}
