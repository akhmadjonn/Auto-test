using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddColorVisionPlatesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ColorVisionPlates",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlateNumber = table.Column<int>(type: "integer", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExpectedAnswer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AlternateAnswer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColorVisionPlates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ColorVisionPlates_SortOrder",
                schema: "autotest",
                table: "ColorVisionPlates",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ColorVisionPlates",
                schema: "autotest");
        }
    }
}
