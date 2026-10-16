using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WATR.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskTypeToTaskItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaskType",
                table: "TaskItems",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaskType",
                table: "TaskItems");
        }
    }
}
