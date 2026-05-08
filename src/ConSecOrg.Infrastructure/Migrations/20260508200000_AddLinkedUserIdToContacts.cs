using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConSecOrg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLinkedUserIdToContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "linked_user_id",
                table: "contacts",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "linked_user_id",
                table: "contacts");
        }
    }
}
