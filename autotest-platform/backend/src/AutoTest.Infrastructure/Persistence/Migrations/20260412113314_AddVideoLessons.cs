using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVideoLessons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VideoCategories",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IconUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "jsonb", nullable: true),
                    Name = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VideoLessons",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    VideoUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsFree = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "jsonb", nullable: true),
                    Title = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoLessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoLessons_VideoCategories_VideoCategoryId",
                        column: x => x.VideoCategoryId,
                        principalSchema: "autotest",
                        principalTable: "VideoCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LessonAttachments",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoLessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonAttachments_VideoLessons_VideoLessonId",
                        column: x => x.VideoLessonId,
                        principalSchema: "autotest",
                        principalTable: "VideoLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLessonProgress",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    VideoLessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    WatchedSeconds = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastWatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLessonProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLessonProgress_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "autotest",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLessonProgress_VideoLessons_VideoLessonId",
                        column: x => x.VideoLessonId,
                        principalSchema: "autotest",
                        principalTable: "VideoLessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LessonAttachments_VideoLessonId",
                schema: "autotest",
                table: "LessonAttachments",
                column: "VideoLessonId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLessonProgress_UserId_VideoLessonId",
                schema: "autotest",
                table: "UserLessonProgress",
                columns: new[] { "UserId", "VideoLessonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLessonProgress_VideoLessonId",
                schema: "autotest",
                table: "UserLessonProgress",
                column: "VideoLessonId");

            migrationBuilder.CreateIndex(
                name: "IX_VideoCategories_SortOrder_IsActive",
                schema: "autotest",
                table: "VideoCategories",
                columns: new[] { "SortOrder", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_VideoLessons_VideoCategoryId_IsActive_SortOrder",
                schema: "autotest",
                table: "VideoLessons",
                columns: new[] { "VideoCategoryId", "IsActive", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LessonAttachments",
                schema: "autotest");

            migrationBuilder.DropTable(
                name: "UserLessonProgress",
                schema: "autotest");

            migrationBuilder.DropTable(
                name: "VideoLessons",
                schema: "autotest");

            migrationBuilder.DropTable(
                name: "VideoCategories",
                schema: "autotest");
        }
    }
}
