using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoadSignsAndMarkings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoadMarkings",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MarkingCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MarkingType = table.Column<int>(type: "integer", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "jsonb", nullable: true),
                    Name = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoadMarkings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoadSignCategories",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    IconUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "jsonb", nullable: false),
                    Name = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoadSignCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoadSigns",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SignCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "jsonb", nullable: true),
                    Name = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoadSigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoadSigns_RoadSignCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "autotest",
                        principalTable: "RoadSignCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoadMarkings_IsActive",
                schema: "autotest",
                table: "RoadMarkings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RoadMarkings_MarkingCode",
                schema: "autotest",
                table: "RoadMarkings",
                column: "MarkingCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoadMarkings_MarkingType",
                schema: "autotest",
                table: "RoadMarkings",
                column: "MarkingType");

            migrationBuilder.CreateIndex(
                name: "IX_RoadMarkings_MarkingType_SortOrder",
                schema: "autotest",
                table: "RoadMarkings",
                columns: new[] { "MarkingType", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RoadSignCategories_Code",
                schema: "autotest",
                table: "RoadSignCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoadSignCategories_IsActive",
                schema: "autotest",
                table: "RoadSignCategories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RoadSignCategories_Slug",
                schema: "autotest",
                table: "RoadSignCategories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoadSigns_CategoryId",
                schema: "autotest",
                table: "RoadSigns",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RoadSigns_CategoryId_SortOrder",
                schema: "autotest",
                table: "RoadSigns",
                columns: new[] { "CategoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_RoadSigns_IsActive",
                schema: "autotest",
                table: "RoadSigns",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RoadSigns_SignCode",
                schema: "autotest",
                table: "RoadSigns",
                column: "SignCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoadMarkings",
                schema: "autotest");

            migrationBuilder.DropTable(
                name: "RoadSigns",
                schema: "autotest");

            migrationBuilder.DropTable(
                name: "RoadSignCategories",
                schema: "autotest");
        }
    }
}
