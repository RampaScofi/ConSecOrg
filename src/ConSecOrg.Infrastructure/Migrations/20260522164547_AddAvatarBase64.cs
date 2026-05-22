using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConSecOrg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAvatarBase64 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "avatar_base64",
                table: "users",
                type: "NVARCHAR(MAX)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "avatar_base64",
                table: "users");
        }
    }
}
