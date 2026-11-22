using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChapGiti.Infrastructure.Persistence.Ef.Migrations;

/// <inheritdoc />
public partial class CutTariffVersions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Cut_Tariff_Versions",
            columns: table => new
            {
                cut_id = table.Column<string>(type: "varchar(36)", unicode: false, maxLength: 36, nullable: false),
                version = table.Column<int>(type: "int", nullable: false),
                updated_at = table.Column<DateTime>(type: "datetime2(3)", precision: 3, nullable: false),
                rates = table.Column<string>(type: "nvarchar(max)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Cut_Tariff_Versions", x => new { x.cut_id, x.version });
                table.CheckConstraint("CK_CutTariff_Rates", "ISJSON([rates]) = 1");
                table.CheckConstraint("CK_CutTariff_Version", "[version] >= 1");
                table.ForeignKey(
                    name: "FK_Cut_Tariff_Versions_Print_Catalog_Options_cut_id",
                    column: x => x.cut_id,
                    principalTable: "Print_Catalog_Options",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TicketMessages_Unread",
            table: "Ticket_Messages",
            columns: new[] { "actor", "ticket_id" },
            filter: "[read_at] IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Cut_Tariff_Versions");

        migrationBuilder.DropIndex(
            name: "IX_TicketMessages_Unread",
            table: "Ticket_Messages");
    }
}
