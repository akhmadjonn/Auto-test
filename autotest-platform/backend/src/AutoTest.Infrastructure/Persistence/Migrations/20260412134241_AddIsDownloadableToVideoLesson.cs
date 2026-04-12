using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDownloadableToVideoLesson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDownloadable",
                schema: "autotest",
                table: "VideoLessons",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDownloadable",
                schema: "autotest",
                table: "VideoLessons");
        }
    }
}
