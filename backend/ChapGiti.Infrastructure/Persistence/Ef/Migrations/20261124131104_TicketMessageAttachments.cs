using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChapGiti.Infrastructure.Persistence.Ef.Migrations
{
    /// <inheritdoc />
    public partial class TicketMessageAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ticket_Attachments",
                columns: table => new
                {
                    id = table.Column<string>(type: "varchar(36)", unicode: false, maxLength: 36, nullable: false),
                    message_id = table.Column<string>(type: "varchar(36)", unicode: false, maxLength: 36, nullable: false),
                    name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    mime = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    digest = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    size = table.Column<int>(type: "int", nullable: false),
                    position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ticket_Attachments", x => x.id);
                    table.CheckConstraint("CK_TicketAttachment_Size", "[size] > 0 AND [position] >= 0");
                    table.ForeignKey(
                        name: "FK_Ticket_Attachments_Ticket_Messages_message_id",
                        column: x => x.message_id,
                        principalTable: "Ticket_Messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_Attachments_message_id_position",
                table: "Ticket_Attachments",
                columns: new[] { "message_id", "position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ticket_Attachments");
        }
    }
}
