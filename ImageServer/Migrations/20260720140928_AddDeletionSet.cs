using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ImageServer.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletionSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FilesToDeletion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrashedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletionAttempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilesToDeletion", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FilesToDeletion");
        }
    }
}
