using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WATR.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionSupportMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "TaskItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "SupersededByHandle",
                table: "TaskItems",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tags",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "SupersededByHandle",
                table: "TaskItems");
        }
    }
}
