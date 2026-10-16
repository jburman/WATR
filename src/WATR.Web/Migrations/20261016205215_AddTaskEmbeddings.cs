using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WATR.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Embedding",
                table: "TaskItems",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmbeddingUpdatedAt",
                table: "TaskItems",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "EmbeddingUpdatedAt",
                table: "TaskItems");
        }
    }
}
