using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConSecOrg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatAndAssignee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedUserId",
                table: "shared_project_tasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SenderUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chat_messages_users_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shared_project_tasks_AssignedUserId",
                table: "shared_project_tasks",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_ProjectId_SentAt",
                table: "chat_messages",
                columns: new[] { "ProjectId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_SenderUserId_ToUserId_SentAt",
                table: "chat_messages",
                columns: new[] { "SenderUserId", "ToUserId", "SentAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_shared_project_tasks_users_AssignedUserId",
                table: "shared_project_tasks",
                column: "AssignedUserId",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shared_project_tasks_users_AssignedUserId",
                table: "shared_project_tasks");

            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropIndex(
                name: "IX_shared_project_tasks_AssignedUserId",
                table: "shared_project_tasks");

            migrationBuilder.DropColumn(
                name: "AssignedUserId",
                table: "shared_project_tasks");
        }
    }
}
