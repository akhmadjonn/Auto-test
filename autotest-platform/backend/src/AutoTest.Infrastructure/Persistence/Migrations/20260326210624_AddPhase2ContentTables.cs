using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2ContentTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FirstAidProcedures",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IconUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "jsonb", nullable: false),
                    Summary = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirstAidProcedures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GlossaryCategories",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Icon = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlossaryCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HazardLabels",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HazardClass = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "jsonb", nullable: false),
                    Name = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HazardLabels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrafficFines",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArticleNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PenaltyAmountTiyins = table.Column<long>(type: "bigint", nullable: false),
                    PenaltyMaxTiyins = table.Column<long>(type: "bigint", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AdditionalNotes = table.Column<string>(type: "jsonb", nullable: true),
                    ViolationDescription = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrafficFines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FirstAidSteps",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstAidProcedureId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepOrder = table.Column<int>(type: "integer", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "jsonb", nullable: false),
                    Title = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FirstAidSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FirstAidSteps_FirstAidProcedures_FirstAidProcedureId",
                        column: x => x.FirstAidProcedureId,
                        principalSchema: "autotest",
                        principalTable: "FirstAidProcedures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GlossaryTerms",
                schema: "autotest",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GlossaryCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    RelatedQuestionIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Definition = table.Column<string>(type: "jsonb", nullable: false),
                    Term = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlossaryTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlossaryTerms_GlossaryCategories_GlossaryCategoryId",
                        column: x => x.GlossaryCategoryId,
                        principalSchema: "autotest",
                        principalTable: "GlossaryCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FirstAidProcedures_Slug",
                schema: "autotest",
                table: "FirstAidProcedures",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FirstAidSteps_FirstAidProcedureId_StepOrder",
                schema: "autotest",
                table: "FirstAidSteps",
                columns: new[] { "FirstAidProcedureId", "StepOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_GlossaryCategories_Slug",
                schema: "autotest",
                table: "GlossaryCategories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlossaryTerms_GlossaryCategoryId",
                schema: "autotest",
                table: "GlossaryTerms",
                column: "GlossaryCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_HazardLabels_Slug",
                schema: "autotest",
                table: "HazardLabels",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrafficFines_ArticleNumber",
                schema: "autotest",
                table: "TrafficFines",
                column: "ArticleNumber");

            migrationBuilder.CreateIndex(
                name: "IX_TrafficFines_IsActive",
                schema: "autotest",
                table: "TrafficFines",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FirstAidSteps",
                schema: "autotest");

            migrationBuilder.DropTable(
                name: "GlossaryTerms",
                schema: "autotest");

            migrationBuilder.DropTable(
                name: "HazardLabels",
                schema: "autotest");

            migrationBuilder.DropTable(
                name: "TrafficFines",
                schema: "autotest");

            migrationBuilder.DropTable(
                name: "FirstAidProcedures",
                schema: "autotest");

            migrationBuilder.DropTable(
                name: "GlossaryCategories",
                schema: "autotest");
        }
    }
}
