using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExamSessionPause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PausedAt",
                schema: "autotest",
                table: "ExamSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RemainingSecondsAtPause",
                schema: "autotest",
                table: "ExamSessions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PausedAt",
                schema: "autotest",
                table: "ExamSessions");

            migrationBuilder.DropColumn(
                name: "RemainingSecondsAtPause",
                schema: "autotest",
                table: "ExamSessions");
        }
    }
}
