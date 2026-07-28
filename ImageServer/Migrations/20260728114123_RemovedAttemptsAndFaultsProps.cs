using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImageServer.Migrations
{
    /// <inheritdoc />
    public partial class RemovedAttemptsAndFaultsProps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletionAttempts",
                table: "FilesToDeletion");

            migrationBuilder.DropColumn(
                name: "DeletionFault",
                table: "FilesToDeletion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DeletionAttempts",
                table: "FilesToDeletion",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "DeletionFault",
                table: "FilesToDeletion",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
