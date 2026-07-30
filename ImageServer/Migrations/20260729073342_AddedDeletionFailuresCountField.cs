using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImageServer.Migrations
{
    /// <inheritdoc />
    public partial class AddedDeletionFailuresCountField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeletionFailures",
                table: "FilesToDeletion",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletionFailures",
                table: "FilesToDeletion");
        }
    }
}
