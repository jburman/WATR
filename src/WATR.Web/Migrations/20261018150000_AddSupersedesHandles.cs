using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WATR.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddSupersedesHandles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupersedesHandles",
                table: "TaskItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SupersedesHandles",
                table: "TaskItems");
        }
    }
}
