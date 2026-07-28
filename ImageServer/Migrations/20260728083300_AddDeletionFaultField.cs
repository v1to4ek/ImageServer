using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImageServer.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletionFaultField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DeletionFault",
                table: "FilesToDeletion",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletionFault",
                table: "FilesToDeletion");
        }
    }
}
