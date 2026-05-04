using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateQuestionStatusFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Questions_CategoryId_Difficulty",
                schema: "autotest",
                table: "Questions");

            // Shift existing rows from old enum to new enum.
            // Old: Draft=0, Active=1, Archived=2.   New: Inactive=1, Active=2.
            // Old Active (1) → new Active (2).
            // Old Archived (2) → new Inactive (1) — was hidden, stays hidden.
            // Old Draft (0)   → new Inactive (1) — was hidden, stays hidden.
            // Done in one CASE expression so the constraint flip from
            // "0 was valid" to "0 is unset" doesn't break existing rows.
            migrationBuilder.Sql("""
                UPDATE autotest."Questions"
                SET "Status" = CASE "Status"
                    WHEN 1 THEN 2
                    WHEN 0 THEN 1
                    WHEN 2 THEN 1
                    ELSE "Status"
                END;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "autotest",
                table: "Questions",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_CategoryId_Difficulty",
                schema: "autotest",
                table: "Questions",
                columns: new[] { "CategoryId", "Difficulty" },
                filter: "\"Status\" = 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Questions_CategoryId_Difficulty",
                schema: "autotest",
                table: "Questions");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "autotest",
                table: "Questions",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            // Reverse the data shift so a downgrade leaves rows in the old enum's
            // shape: new Active (2) → old Active (1); new Inactive (1) → old Draft (0).
            // (We can't recover the original Active-vs-Archived split — both were
            // collapsed to Inactive on Up.)
            migrationBuilder.Sql("""
                UPDATE autotest."Questions"
                SET "Status" = CASE "Status"
                    WHEN 2 THEN 1
                    WHEN 1 THEN 0
                    ELSE "Status"
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Questions_CategoryId_Difficulty",
                schema: "autotest",
                table: "Questions",
                columns: new[] { "CategoryId", "Difficulty" },
                filter: "\"Status\" = 1");
        }
    }
}
