using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WATR.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddHandleAndNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ProjectId",
                table: "TaskItems");

            migrationBuilder.AddColumn<string>(
                name: "TaskHandle",
                table: "TaskItems",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TaskNumber",
                table: "TaskItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ProjectId_TaskHandle",
                table: "TaskItems",
                columns: new[] { "ProjectId", "TaskHandle" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ProjectId_TaskNumber",
                table: "TaskItems",
                columns: new[] { "ProjectId", "TaskNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ProjectId_TaskHandle",
                table: "TaskItems");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_ProjectId_TaskNumber",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "TaskHandle",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "TaskNumber",
                table: "TaskItems");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ProjectId",
                table: "TaskItems",
                column: "ProjectId");
        }
    }
}
