using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConSecOrg.Infrastructure.Migrations
{
    public partial class AddChatMessageEncryption : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "text_cipher",
                table: "chat_messages",
                type: "VARBINARY(MAX)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "text_nonce",
                table: "chat_messages",
                type: "VARBINARY(16)",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "text_hmac",
                table: "chat_messages",
                type: "VARBINARY(32)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "text_cipher", table: "chat_messages");
            migrationBuilder.DropColumn(name: "text_nonce", table: "chat_messages");
            migrationBuilder.DropColumn(name: "text_hmac", table: "chat_messages");
        }
    }
}
