using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WATR.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTaskTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaskTypes",
                table: "Projects",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaskTypes",
                table: "Projects");
        }
    }
}
