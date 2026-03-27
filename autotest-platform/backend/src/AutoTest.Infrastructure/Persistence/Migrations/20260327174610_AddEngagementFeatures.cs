using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagementFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentStreak",
                schema: "autotest",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastStudyDate",
                schema: "autotest",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                schema: "autotest",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "LongestStreak",
                schema: "autotest",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "TotalXp",
                schema: "autotest",
                table: "Users",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "LeaderboardSnapshots",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SnapshotDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    XpValue = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaderboardSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaderboardSnapshots_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "autotest",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserDailyStats",
                schema: "autotest",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatDate = table.Column<DateOnly>(type: "date", nullable: false),
                    QuestionsAnswered = table.Column<int>(type: "integer", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "integer", nullable: false),
                    XpEarned = table.Column<long>(type: "bigint", nullable: false),
                    ExamsCompleted = table.Column<int>(type: "integer", nullable: false),
                    TimeSpentSeconds = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDailyStats", x => new { x.UserId, x.StatDate });
                    table.ForeignKey(
                        name: "FK_UserDailyStats_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "autotest",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserFavoriteQuestions",
                schema: "autotest",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFavoriteQuestions", x => new { x.UserId, x.QuestionId });
                    table.ForeignKey(
                        name: "FK_UserFavoriteQuestions_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalSchema: "autotest",
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserFavoriteQuestions_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "autotest",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSettings",
                schema: "autotest",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => new { x.UserId, x.Key });
                    table.ForeignKey(
                        name: "FK_UserSettings_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "autotest",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSnapshots_Period_SnapshotDate_Rank",
                schema: "autotest",
                table: "LeaderboardSnapshots",
                columns: new[] { "Period", "SnapshotDate", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaderboardSnapshots_UserId_Period",
                schema: "autotest",
                table: "LeaderboardSnapshots",
                columns: new[] { "UserId", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_UserDailyStats_UserId_StatDate",
                schema: "autotest",
                table: "UserDailyStats",
                columns: new[] { "UserId", "StatDate" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_UserFavoriteQuestions_QuestionId",
                schema: "autotest",
                table: "UserFavoriteQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFavoriteQuestions_UserId",
                schema: "autotest",
                table: "UserFavoriteQuestions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaderboardSnapshots",
                schema: "autotest");

            migrationBuilder.DropTable(
                name: "UserDailyStats",
                schema: "autotest");

            migrationBuilder.DropTable(
                name: "UserFavoriteQuestions",
                schema: "autotest");

            migrationBuilder.DropTable(
                name: "UserSettings",
                schema: "autotest");

            migrationBuilder.DropColumn(
                name: "CurrentStreak",
                schema: "autotest",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastStudyDate",
                schema: "autotest",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Level",
                schema: "autotest",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LongestStreak",
                schema: "autotest",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TotalXp",
                schema: "autotest",
                table: "Users");
        }
    }
}
