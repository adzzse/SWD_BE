using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPacketWorkflowCoreSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "submission",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExamId = table.Column<long>(type: "bigint", nullable: false),
                    ExamStudentId = table.Column<long>(type: "bigint", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    OriginalFileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SourceFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    FailureReason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submission_exam_ExamId",
                        column: x => x.ExamId,
                        principalTable: "exam",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_submission_exam_student_ExamStudentId",
                        column: x => x.ExamStudentId,
                        principalTable: "exam_student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "processing_job",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubmissionId = table.Column<long>(type: "bigint", nullable: false),
                    JobType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processing_job", x => x.Id);
                    table.ForeignKey(
                        name: "FK_processing_job_submission_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_packet",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SubmissionId = table.Column<long>(type: "bigint", nullable: false),
                    ExamId = table.Column<long>(type: "bigint", nullable: false),
                    ExamStudentId = table.Column<long>(type: "bigint", nullable: false),
                    ExamQuestionId = table.Column<long>(type: "bigint", nullable: true),
                    QuestionNumber = table.Column<int>(type: "integer", nullable: false),
                    ExtractedAnswerText = table.Column<string>(type: "TEXT", nullable: true),
                    PrimaryImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ImageUrlsJson = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ParseNotes = table.Column<string>(type: "TEXT", nullable: true),
                    ParseConfidence = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_packet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_packet_exam_ExamId",
                        column: x => x.ExamId,
                        principalTable: "exam",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_question_packet_exam_question_ExamQuestionId",
                        column: x => x.ExamQuestionId,
                        principalTable: "exam_question",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_question_packet_exam_student_ExamStudentId",
                        column: x => x.ExamStudentId,
                        principalTable: "exam_student",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_question_packet_submission_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submission",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "flag",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    QuestionPacketId = table.Column<long>(type: "bigint", nullable: false),
                    MatchedQuestionPacketId = table.Column<long>(type: "bigint", nullable: false),
                    SimilarityScore = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    ThresholdUsed = table.Column<decimal>(type: "numeric(5,4)", nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TeacherDecision = table.Column<bool>(type: "boolean", nullable: true),
                    TeacherNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flag", x => x.Id);
                    table.ForeignKey(
                        name: "FK_flag_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_flag_question_packet_MatchedQuestionPacketId",
                        column: x => x.MatchedQuestionPacketId,
                        principalTable: "question_packet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_flag_question_packet_QuestionPacketId",
                        column: x => x.QuestionPacketId,
                        principalTable: "question_packet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_flag_MatchedQuestionPacketId",
                table: "flag",
                column: "MatchedQuestionPacketId");

            migrationBuilder.CreateIndex(
                name: "IX_flag_QuestionPacketId_MatchedQuestionPacketId",
                table: "flag",
                columns: new[] { "QuestionPacketId", "MatchedQuestionPacketId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_flag_ReviewedByUserId",
                table: "flag",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_processing_job_SubmissionId_JobType_CreatedAt",
                table: "processing_job",
                columns: new[] { "SubmissionId", "JobType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_question_packet_ExamId_ExamStudentId_QuestionNumber",
                table: "question_packet",
                columns: new[] { "ExamId", "ExamStudentId", "QuestionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_question_packet_ExamQuestionId",
                table: "question_packet",
                column: "ExamQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_question_packet_ExamStudentId",
                table: "question_packet",
                column: "ExamStudentId");

            migrationBuilder.CreateIndex(
                name: "IX_question_packet_SubmissionId_QuestionNumber",
                table: "question_packet",
                columns: new[] { "SubmissionId", "QuestionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submission_ExamId_ExamStudentId_Attempt",
                table: "submission",
                columns: new[] { "ExamId", "ExamStudentId", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submission_ExamStudentId",
                table: "submission",
                column: "ExamStudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "flag");

            migrationBuilder.DropTable(
                name: "processing_job");

            migrationBuilder.DropTable(
                name: "question_packet");

            migrationBuilder.DropTable(
                name: "submission");
        }
    }
}
