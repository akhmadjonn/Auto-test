using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpeedChallengeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TimeLimitPerQuestionSeconds",
                schema: "autotest",
                table: "ExamTemplates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimeLimitPerQuestionSeconds",
                schema: "autotest",
                table: "ExamSessions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeLimitPerQuestionSeconds",
                schema: "autotest",
                table: "ExamTemplates");

            migrationBuilder.DropColumn(
                name: "TimeLimitPerQuestionSeconds",
                schema: "autotest",
                table: "ExamSessions");
        }
    }
}
