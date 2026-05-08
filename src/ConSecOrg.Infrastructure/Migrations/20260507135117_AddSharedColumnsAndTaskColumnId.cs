using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ConSecOrg.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedColumnsAndTaskColumnId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ColumnId",
                table: "shared_project_tasks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "shared_project_tasks",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateIndex(
                name: "IX_shared_project_tasks_ColumnId",
                table: "shared_project_tasks",
                column: "ColumnId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_shared_project_tasks_ColumnId",
                table: "shared_project_tasks");

            migrationBuilder.DropColumn(
                name: "ColumnId",
                table: "shared_project_tasks");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "shared_project_tasks");
        }
    }
}
