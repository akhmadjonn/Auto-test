using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase15TechnicalHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamPoolRules_Categories_CategoryId",
                schema: "autotest",
                table: "ExamPoolRules");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamSessions_ExamTemplates_ExamTemplateId",
                schema: "autotest",
                table: "ExamSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamSessions_Users_UserId",
                schema: "autotest",
                table: "ExamSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_Subscriptions_SubscriptionId",
                schema: "autotest",
                table: "PaymentTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_Users_UserId",
                schema: "autotest",
                table: "PaymentTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Categories_CategoryId",
                schema: "autotest",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_SessionQuestions_Questions_QuestionId",
                schema: "autotest",
                table: "SessionQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_SubscriptionPlans_PlanId",
                schema: "autotest",
                table: "Subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Users_UserId",
                schema: "autotest",
                table: "Subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCategoryStats_Categories_CategoryId",
                schema: "autotest",
                table: "UserCategoryStats");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCategoryStats_Users_UserId",
                schema: "autotest",
                table: "UserCategoryStats");

            migrationBuilder.DropForeignKey(
                name: "FK_UserQuestionStates_Questions_QuestionId",
                schema: "autotest",
                table: "UserQuestionStates");

            migrationBuilder.DropForeignKey(
                name: "FK_UserQuestionStates_Users_UserId",
                schema: "autotest",
                table: "UserQuestionStates");

            migrationBuilder.DropIndex(
                name: "IX_SessionQuestions_QuestionId",
                schema: "autotest",
                table: "SessionQuestions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_CategoryId_Difficulty",
                schema: "autotest",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_IsActive",
                schema: "autotest",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_TicketNumber_IsActive",
                schema: "autotest",
                table: "Questions");

            // Add Status BEFORE dropping IsActive so we can migrate data
            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "autotest",
                table: "Questions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Migrate data: IsActive=true → Status=1 (Active), IsActive=false → Status=2 (Archived)
            migrationBuilder.Sql("""
                UPDATE autotest."Questions"
                SET "Status" = CASE WHEN "IsActive" = true THEN 1 ELSE 2 END
                """);

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "autotest",
                table: "Questions");

            migrationBuilder.AddColumn<double>(
                name: "AvgTimeSec",
                schema: "autotest",
                table: "Questions",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CorrectCount",
                schema: "autotest",
                table: "Questions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalAttempts",
                schema: "autotest",
                table: "Questions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // PostgreSQL can't auto-cast varchar/text → jsonb, need explicit USING cast
            migrationBuilder.Sql("""
                ALTER TABLE autotest."SubscriptionPlans"
                ALTER COLUMN "Features" TYPE jsonb USING "Features"::jsonb
                """);

            migrationBuilder.Sql("""
                ALTER TABLE autotest."AuditLogs"
                ALTER COLUMN "OldValues" TYPE jsonb USING "OldValues"::jsonb
                """);

            migrationBuilder.Sql("""
                ALTER TABLE autotest."AuditLogs"
                ALTER COLUMN "NewValues" TYPE jsonb USING "NewValues"::jsonb
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SessionQuestions_QuestionId_ExamSessionId",
                schema: "autotest",
                table: "SessionQuestions",
                columns: new[] { "QuestionId", "ExamSessionId" },
                filter: "\"IsCorrect\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_CategoryId_Difficulty",
                schema: "autotest",
                table: "Questions",
                columns: new[] { "CategoryId", "Difficulty" },
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_Status",
                schema: "autotest",
                table: "Questions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_TicketNumber_Status",
                schema: "autotest",
                table: "Questions",
                columns: new[] { "TicketNumber", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamSessions_CompletedAt",
                schema: "autotest",
                table: "ExamSessions",
                column: "CompletedAt",
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ExamSessions_UserId_CreatedAt",
                schema: "autotest",
                table: "ExamSessions",
                columns: new[] { "UserId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.AddForeignKey(
                name: "FK_ExamPoolRules_Categories_CategoryId",
                schema: "autotest",
                table: "ExamPoolRules",
                column: "CategoryId",
                principalSchema: "autotest",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamSessions_ExamTemplates_ExamTemplateId",
                schema: "autotest",
                table: "ExamSessions",
                column: "ExamTemplateId",
                principalSchema: "autotest",
                principalTable: "ExamTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamSessions_Users_UserId",
                schema: "autotest",
                table: "ExamSessions",
                column: "UserId",
                principalSchema: "autotest",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_Subscriptions_SubscriptionId",
                schema: "autotest",
                table: "PaymentTransactions",
                column: "SubscriptionId",
                principalSchema: "autotest",
                principalTable: "Subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_Users_UserId",
                schema: "autotest",
                table: "PaymentTransactions",
                column: "UserId",
                principalSchema: "autotest",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Categories_CategoryId",
                schema: "autotest",
                table: "Questions",
                column: "CategoryId",
                principalSchema: "autotest",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SessionQuestions_Questions_QuestionId",
                schema: "autotest",
                table: "SessionQuestions",
                column: "QuestionId",
                principalSchema: "autotest",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_SubscriptionPlans_PlanId",
                schema: "autotest",
                table: "Subscriptions",
                column: "PlanId",
                principalSchema: "autotest",
                principalTable: "SubscriptionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Users_UserId",
                schema: "autotest",
                table: "Subscriptions",
                column: "UserId",
                principalSchema: "autotest",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCategoryStats_Categories_CategoryId",
                schema: "autotest",
                table: "UserCategoryStats",
                column: "CategoryId",
                principalSchema: "autotest",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCategoryStats_Users_UserId",
                schema: "autotest",
                table: "UserCategoryStats",
                column: "UserId",
                principalSchema: "autotest",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserQuestionStates_Questions_QuestionId",
                schema: "autotest",
                table: "UserQuestionStates",
                column: "QuestionId",
                principalSchema: "autotest",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserQuestionStates_Users_UserId",
                schema: "autotest",
                table: "UserQuestionStates",
                column: "UserId",
                principalSchema: "autotest",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamPoolRules_Categories_CategoryId",
                schema: "autotest",
                table: "ExamPoolRules");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamSessions_ExamTemplates_ExamTemplateId",
                schema: "autotest",
                table: "ExamSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamSessions_Users_UserId",
                schema: "autotest",
                table: "ExamSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_Subscriptions_SubscriptionId",
                schema: "autotest",
                table: "PaymentTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_Users_UserId",
                schema: "autotest",
                table: "PaymentTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Categories_CategoryId",
                schema: "autotest",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_SessionQuestions_Questions_QuestionId",
                schema: "autotest",
                table: "SessionQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_SubscriptionPlans_PlanId",
                schema: "autotest",
                table: "Subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_Users_UserId",
                schema: "autotest",
                table: "Subscriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCategoryStats_Categories_CategoryId",
                schema: "autotest",
                table: "UserCategoryStats");

            migrationBuilder.DropForeignKey(
                name: "FK_UserCategoryStats_Users_UserId",
                schema: "autotest",
                table: "UserCategoryStats");

            migrationBuilder.DropForeignKey(
                name: "FK_UserQuestionStates_Questions_QuestionId",
                schema: "autotest",
                table: "UserQuestionStates");

            migrationBuilder.DropForeignKey(
                name: "FK_UserQuestionStates_Users_UserId",
                schema: "autotest",
                table: "UserQuestionStates");

            migrationBuilder.DropIndex(
                name: "IX_SessionQuestions_QuestionId_ExamSessionId",
                schema: "autotest",
                table: "SessionQuestions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_CategoryId_Difficulty",
                schema: "autotest",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_Status",
                schema: "autotest",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_TicketNumber_Status",
                schema: "autotest",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_ExamSessions_CompletedAt",
                schema: "autotest",
                table: "ExamSessions");

            migrationBuilder.DropIndex(
                name: "IX_ExamSessions_UserId_CreatedAt",
                schema: "autotest",
                table: "ExamSessions");

            migrationBuilder.DropColumn(
                name: "AvgTimeSec",
                schema: "autotest",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "CorrectCount",
                schema: "autotest",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "autotest",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "TotalAttempts",
                schema: "autotest",
                table: "Questions");

            // Revert jsonb → varchar/text with explicit USING cast
            migrationBuilder.Sql("""
                ALTER TABLE autotest."SubscriptionPlans"
                ALTER COLUMN "Features" TYPE character varying(4000) USING "Features"::text
                """);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "autotest",
                table: "Questions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                ALTER TABLE autotest."AuditLogs"
                ALTER COLUMN "OldValues" TYPE text USING "OldValues"::text
                """);

            migrationBuilder.Sql("""
                ALTER TABLE autotest."AuditLogs"
                ALTER COLUMN "NewValues" TYPE text USING "NewValues"::text
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SessionQuestions_QuestionId",
                schema: "autotest",
                table: "SessionQuestions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_CategoryId_Difficulty",
                schema: "autotest",
                table: "Questions",
                columns: new[] { "CategoryId", "Difficulty" },
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_IsActive",
                schema: "autotest",
                table: "Questions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_TicketNumber_IsActive",
                schema: "autotest",
                table: "Questions",
                columns: new[] { "TicketNumber", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_ExamPoolRules_Categories_CategoryId",
                schema: "autotest",
                table: "ExamPoolRules",
                column: "CategoryId",
                principalSchema: "autotest",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamSessions_ExamTemplates_ExamTemplateId",
                schema: "autotest",
                table: "ExamSessions",
                column: "ExamTemplateId",
                principalSchema: "autotest",
                principalTable: "ExamTemplates",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamSessions_Users_UserId",
                schema: "autotest",
                table: "ExamSessions",
                column: "UserId",
                principalSchema: "autotest",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_Subscriptions_SubscriptionId",
                schema: "autotest",
                table: "PaymentTransactions",
                column: "SubscriptionId",
                principalSchema: "autotest",
                principalTable: "Subscriptions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_Users_UserId",
                schema: "autotest",
                table: "PaymentTransactions",
                column: "UserId",
                principalSchema: "autotest",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Categories_CategoryId",
                schema: "autotest",
                table: "Questions",
                column: "CategoryId",
                principalSchema: "autotest",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SessionQuestions_Questions_QuestionId",
                schema: "autotest",
                table: "SessionQuestions",
                column: "QuestionId",
                principalSchema: "autotest",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_SubscriptionPlans_PlanId",
                schema: "autotest",
                table: "Subscriptions",
                column: "PlanId",
                principalSchema: "autotest",
                principalTable: "SubscriptionPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_Users_UserId",
                schema: "autotest",
                table: "Subscriptions",
                column: "UserId",
                principalSchema: "autotest",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCategoryStats_Categories_CategoryId",
                schema: "autotest",
                table: "UserCategoryStats",
                column: "CategoryId",
                principalSchema: "autotest",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCategoryStats_Users_UserId",
                schema: "autotest",
                table: "UserCategoryStats",
                column: "UserId",
                principalSchema: "autotest",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserQuestionStates_Questions_QuestionId",
                schema: "autotest",
                table: "UserQuestionStates",
                column: "QuestionId",
                principalSchema: "autotest",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserQuestionStates_Users_UserId",
                schema: "autotest",
                table: "UserQuestionStates",
                column: "UserId",
                principalSchema: "autotest",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
