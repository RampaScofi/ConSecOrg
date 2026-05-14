using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConSecOrg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupChatAndFileAttachment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileId",
                table: "chat_messages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentFileName",
                table: "chat_messages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AttachmentSize",
                table: "chat_messages",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "GroupChatId",
                table: "chat_messages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "group_chats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_chats", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "group_chat_members",
                columns: table => new
                {
                    GroupChatId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_chat_members", x => new { x.GroupChatId, x.UserId });
                    table.ForeignKey(
                        name: "FK_group_chat_members_group_chats_GroupChatId",
                        column: x => x.GroupChatId,
                        principalTable: "group_chats",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_chat_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_GroupChatId",
                table: "chat_messages",
                column: "GroupChatId");

            migrationBuilder.CreateIndex(
                name: "IX_group_chat_members_UserId",
                table: "group_chat_members",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_chat_messages_group_chats_GroupChatId",
                table: "chat_messages",
                column: "GroupChatId",
                principalTable: "group_chats",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chat_messages_group_chats_GroupChatId",
                table: "chat_messages");

            migrationBuilder.DropTable(
                name: "group_chat_members");

            migrationBuilder.DropTable(
                name: "group_chats");

            migrationBuilder.DropIndex(
                name: "IX_chat_messages_GroupChatId",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "AttachmentFileId",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "AttachmentFileName",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "AttachmentSize",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "GroupChatId",
                table: "chat_messages");
        }
    }
}
