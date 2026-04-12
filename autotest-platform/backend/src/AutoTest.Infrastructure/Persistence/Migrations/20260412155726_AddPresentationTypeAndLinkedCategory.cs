using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AutoTest.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPresentationTypeAndLinkedCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LinkedCategoryId",
                schema: "autotest",
                table: "VideoLessons",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoLessons_LinkedCategoryId",
                schema: "autotest",
                table: "VideoLessons",
                column: "LinkedCategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoLessons_Categories_LinkedCategoryId",
                schema: "autotest",
                table: "VideoLessons",
                column: "LinkedCategoryId",
                principalSchema: "autotest",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VideoLessons_Categories_LinkedCategoryId",
                schema: "autotest",
                table: "VideoLessons");

            migrationBuilder.DropIndex(
                name: "IX_VideoLessons_LinkedCategoryId",
                schema: "autotest",
                table: "VideoLessons");

            migrationBuilder.DropColumn(
                name: "LinkedCategoryId",
                schema: "autotest",
                table: "VideoLessons");
        }
    }
}
