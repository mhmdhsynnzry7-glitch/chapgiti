using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChapGiti.Infrastructure.Persistence.Ef.Migrations
{
    /// <inheritdoc />
    public partial class TicketMessageReadState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "read_at",
                table: "Ticket_Messages",
                type: "datetime2(3)",
                precision: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "read_at",
                table: "Ticket_Messages");
        }
    }
}
