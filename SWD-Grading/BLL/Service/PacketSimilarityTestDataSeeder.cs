using BLL.Interface;
using DAL;
using Microsoft.EntityFrameworkCore;
using Model.Entity;
using Model.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Service
{
    public class PacketSimilarityTestDataSeeder : IPacketSimilarityTestDataSeeder
    {
        private readonly SWDGradingDbContext _context;

        public PacketSimilarityTestDataSeeder(SWDGradingDbContext context)
        {
            _context = context;
        }

        public async Task<string> SeedAsync(long examId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var exam = await _context.Exams
                .Include(x => x.ExamStudents)
                .FirstOrDefaultAsync(x => x.Id == examId);

            if (exam == null)
            {
                return $"Exam {examId} not found.";
            }

            var existingPackets = await _context.QuestionPackets
                .AnyAsync(x => x.ExamId == examId);

            if (existingPackets)
            {
                return $"Exam {examId} already has QuestionPackets. Seed skipped.";
            }

            var students = exam.ExamStudents?
                .OrderBy(x => x.Id)
                .Take(2)
                .ToList();

            if (students == null || students.Count < 2)
            {
                return $"Exam {examId} must have at least 2 ExamStudents to seed packet similarity test data.";
            }

            var studentA = students[0];
            var studentB = students[1];

            var now = DateTime.UtcNow;
            var attemptA = await GetNextSubmissionAttemptAsync(examId, studentA.Id);
            var attemptB = await GetNextSubmissionAttemptAsync(examId, studentB.Id);

            var submissionA = new Submission
            {
                ExamId = examId,
                ExamStudentId = studentA.Id,
                OriginalFileName = $"seed-student-{studentA.Id}.docx",
                OriginalFileUrl = $"seed://exam-{examId}/student-{studentA.Id}.docx",
                SourceFormat = "DOCX",
                Attempt = attemptA,
                Status = SubmissionStatus.COMPLETED,
                CreatedAt = now,
                UpdatedAt = now
            };

            var submissionB = new Submission
            {
                ExamId = examId,
                ExamStudentId = studentB.Id,
                OriginalFileName = $"seed-student-{studentB.Id}.docx",
                OriginalFileUrl = $"seed://exam-{examId}/student-{studentB.Id}.docx",
                SourceFormat = "DOCX",
                Attempt = attemptB,
                Status = SubmissionStatus.COMPLETED,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Submissions.AddRange(submissionA, submissionB);
            await _context.SaveChangesAsync();

            var packets = new List<QuestionPacket>
            {
                new QuestionPacket
                {
                    SubmissionId = submissionA.Id,
                    ExamId = examId,
                    ExamStudentId = studentA.Id,
                    QuestionNumber = 1,
                    ExtractedAnswerText = "TCP uses a connection-oriented mechanism, provides reliable delivery, sequencing, and retransmission when packets are lost.",
                    PrimaryImageUrl = null,
                    ImageUrlsJson = "[]",
                    Status = QuestionPacketStatus.READY,
                    ParseConfidence = 0.98m,
                    ParseNotes = "Seeded test packet",
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new QuestionPacket
                {
                    SubmissionId = submissionB.Id,
                    ExamId = examId,
                    ExamStudentId = studentB.Id,
                    QuestionNumber = 1,
                    ExtractedAnswerText = "TCP is connection-oriented, ensures reliable delivery, preserves order, and retransmits lost packets.",
                    PrimaryImageUrl = null,
                    ImageUrlsJson = "[]",
                    Status = QuestionPacketStatus.READY,
                    ParseConfidence = 0.97m,
                    ParseNotes = "Seeded near-duplicate packet",
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new QuestionPacket
                {
                    SubmissionId = submissionA.Id,
                    ExamId = examId,
                    ExamStudentId = studentA.Id,
                    QuestionNumber = 2,
                    ExtractedAnswerText = "Normalization scales values into a similar range so models can train more stably and compare features more fairly.",
                    PrimaryImageUrl = null,
                    ImageUrlsJson = "[]",
                    Status = QuestionPacketStatus.READY,
                    ParseConfidence = 0.95m,
                    ParseNotes = "Seeded packet",
                    CreatedAt = now,
                    UpdatedAt = now
                },
                new QuestionPacket
                {
                    SubmissionId = submissionB.Id,
                    ExamId = examId,
                    ExamStudentId = studentB.Id,
                    QuestionNumber = 2,
                    ExtractedAnswerText = "A binary search tree stores ordered values where left children are smaller and right children are greater than the parent.",
                    PrimaryImageUrl = null,
                    ImageUrlsJson = "[]",
                    Status = QuestionPacketStatus.READY,
                    ParseConfidence = 0.96m,
                    ParseNotes = "Seeded different packet",
                    CreatedAt = now,
                    UpdatedAt = now
                }
            };

            _context.QuestionPackets.AddRange(packets);
            await _context.SaveChangesAsync();

            var seedFlag = new Flag
            {
                QuestionPacketId = packets[0].Id,
                MatchedQuestionPacketId = packets[1].Id,
                SimilarityScore = 0.91m,
                ThresholdUsed = 0.80m,
                Source = SimilarityScope.SameQuestion,
                ReviewStatus = FlagReviewStatus.PENDING,
                TeacherDecision = null,
                TeacherNotes = null,
                CreatedAt = now
            };

            _context.Flags.Add(seedFlag);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return $"Seeded 2 submissions, 4 question packets, and 1 sample flag for exam {examId}.";
        }

        private async Task<int> GetNextSubmissionAttemptAsync(long examId, long examStudentId)
        {
            var latestAttempt = await _context.Submissions
                .Where(x => x.ExamId == examId && x.ExamStudentId == examStudentId)
                .Select(x => (int?)x.Attempt)
                .MaxAsync();

            return (latestAttempt ?? 0) + 1;
        }
    }
}
