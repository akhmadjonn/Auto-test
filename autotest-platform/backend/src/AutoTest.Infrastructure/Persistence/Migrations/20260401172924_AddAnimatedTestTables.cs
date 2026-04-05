using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnimatedTestTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnimatedTestSessions",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TotalQuestions = table.Column<short>(type: "smallint", nullable: false),
                    CorrectCount = table.Column<short>(type: "smallint", nullable: false),
                    ScorePercentage = table.Column<short>(type: "smallint", nullable: false),
                    QuestionIds = table.Column<string[]>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimatedTestSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnimatedTestSessions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "autotest",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AnimatedTestAnswers",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SelectedOptionId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TimeSpentMs = table.Column<int>(type: "integer", nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnimatedTestAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnimatedTestAnswers_AnimatedTestSessions_SessionId",
                        column: x => x.SessionId,
                        principalSchema: "autotest",
                        principalTable: "AnimatedTestSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnimatedTestAnswers_SessionId",
                schema: "autotest",
                table: "AnimatedTestAnswers",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AnimatedTestSessions_UserId_CreatedAt",
                schema: "autotest",
                table: "AnimatedTestSessions",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnimatedTestAnswers",
                schema: "autotest");

            migrationBuilder.DropTable(
                name: "AnimatedTestSessions",
                schema: "autotest");
        }
    }
}
