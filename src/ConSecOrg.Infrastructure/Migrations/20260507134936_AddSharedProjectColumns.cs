using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConSecOrg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedProjectColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shared_project_columns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_project_columns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_shared_project_columns_shared_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "shared_projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_shared_project_columns_ProjectId_Order",
                table: "shared_project_columns",
                columns: new[] { "ProjectId", "Order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shared_project_columns");
        }
    }
}
