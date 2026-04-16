using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPacketSimilarityReviewFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_flag_QuestionPacketId_MatchedQuestionPacketId",
                table: "flag");

            migrationBuilder.CreateIndex(
                name: "IX_flag_QuestionPacketId_MatchedQuestionPacketId_Source",
                table: "flag",
                columns: new[] { "QuestionPacketId", "MatchedQuestionPacketId", "Source" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_flag_QuestionPacketId_MatchedQuestionPacketId_Source",
                table: "flag");

            migrationBuilder.CreateIndex(
                name: "IX_flag_QuestionPacketId_MatchedQuestionPacketId",
                table: "flag",
                columns: new[] { "QuestionPacketId", "MatchedQuestionPacketId" },
                unique: true);
        }
    }
}
