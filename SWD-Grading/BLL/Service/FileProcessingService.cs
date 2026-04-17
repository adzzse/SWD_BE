using BLL.Interface;
using DAL.Interface;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Logging;
using Model.Entity;
using Model.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Service
{
	public class FileProcessingService : IFileProcessingService
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly IS3Service _s3Service;
		private readonly IExamService _examService;
		private readonly ILogger<FileProcessingService> _logger;
		private readonly ITesseractOcrService _tesseractOcrService;
		
		public FileProcessingService(IUnitOfWork unitOfWork, IS3Service s3Service, IExamService examService, ITesseractOcrService tesseractOcrService, ILogger<FileProcessingService> logger)
		{
			_unitOfWork = unitOfWork;
			_s3Service = s3Service;
			_examService = examService;
			_tesseractOcrService = tesseractOcrService;
			_logger = logger;
		}

		public async Task ProcessStudentSolutionsAsync(long examZipId)
		{
			var processSummary = new StringBuilder();
			int processedCount = 0;
			int successCount = 0;
			int errorCount = 0;
			var errors = new List<string>();

			try
			{
				// Get ExamZip record
				var examZip = await _unitOfWork.ExamZipRepository.GetByIdAsync(examZipId);
				if (examZip == null)
				{
					throw new Exception($"ExamZip with ID {examZipId} not found");
				}

				// Get Exam info
				var exam = await _unitOfWork.ExamRepository.GetByIdAsync(examZip.ExamId);
				if (exam == null)
				{
					throw new Exception($"Exam with ID {examZip.ExamId} not found");
				}

				// Check if ZIP file exists
				if (string.IsNullOrEmpty(examZip.ZipPath) || !File.Exists(examZip.ZipPath))
				{
					examZip.ParseStatus = ParseStatus.ERROR;
					examZip.ParseSummary = "ZIP file not found at specified path";
					await _unitOfWork.SaveChangesAsync();
					return;
				}

				// Create temp extraction directory
				var tempExtractPath = Path.Combine(Path.GetTempPath(), $"exam_{examZipId}_{Guid.NewGuid()}");
				Directory.CreateDirectory(tempExtractPath);

				try
				{
					// Extract main ZIP file
					ZipFile.ExtractToDirectory(examZip.ZipPath, tempExtractPath);
					examZip.ExtractedPath = tempExtractPath;

					// Find Student_Solutions folder (it might be at root or inside another folder)
					string studentSolutionsPath = tempExtractPath;
					var possiblePaths = new[]
					{
						Path.Combine(tempExtractPath, "Student_Solutions"),
						tempExtractPath
					};

					// Try to find Student_Solutions folder
					foreach (var path in possiblePaths)
					{
						if (Directory.Exists(path))
						{
							var dirs = Directory.GetDirectories(path);
							if (dirs.Length > 0)
							{
								studentSolutionsPath = path;
								break;
							}
						}
					}

					//--------------------------------------------------------------------
					// NEW: SCAN FOR EXAM PAPER (.docx in root)
					//--------------------------------------------------------------------
					var docxInRoot = Directory.GetFiles(tempExtractPath, "*.docx", SearchOption.TopDirectoryOnly)
						.Where(f => !Path.GetFileName(f).StartsWith("~$"))
						.FirstOrDefault();

					if (docxInRoot != null)
					{
						processSummary.AppendLine($"Detected exam paper: {Path.GetFileName(docxInRoot)}. Parsing questions...");
						try 
						{
							int qCount = await _examService.ParseDocxQuestionsFromPath(exam.Id, docxInRoot);
							processSummary.AppendLine($"Successfully parsed {qCount} questions from DOCX.");
						}
						catch (Exception ex)
						{
							processSummary.AppendLine($"Warning: Failed to parse questions from DOCX: {ex.Message}");
						}
					}

					// Get all student folders
					var studentFolders = Directory.GetDirectories(studentSolutionsPath);
					processSummary.AppendLine($"Found {studentFolders.Length} student folders");

					foreach (var studentFolder in studentFolders)
					{
						processedCount++;
						var studentFolderName = Path.GetFileName(studentFolder);

						try
						{
							await ProcessStudentFolderAsync(studentFolder, studentFolderName, examZip, exam);
							successCount++;
						}
						catch (Exception ex)
						{
							errorCount++;
							var errorMsg = $"Error processing {studentFolderName}: {ex.Message}";
							errors.Add(errorMsg);
							processSummary.AppendLine(errorMsg);
						}
					}

					// Update ExamZip status
					examZip.ParseStatus = errorCount == processedCount ? ParseStatus.ERROR : ParseStatus.DONE;
					processSummary.AppendLine($"\nProcessing complete:");
					processSummary.AppendLine($"Total: {processedCount}");
					processSummary.AppendLine($"Success: {successCount}");
					processSummary.AppendLine($"Errors: {errorCount}");
					examZip.ParseSummary = processSummary.ToString();

					await _unitOfWork.SaveChangesAsync();
				}
				finally
				{
					// Cleanup temp directory after processing
					if (!string.IsNullOrEmpty(tempExtractPath) && Directory.Exists(tempExtractPath))
					{
						try
						{
							Directory.Delete(tempExtractPath, true);
						}
						catch (Exception ex)
						{
							Console.WriteLine($"Error cleaning up temp directory: {ex.Message}");
						}
					}

					// Delete uploaded ZIP file
					if (!string.IsNullOrEmpty(examZip.ZipPath) && File.Exists(examZip.ZipPath))
					{
						try
						{
							File.Delete(examZip.ZipPath);
						}
						catch (Exception ex)
						{
							Console.WriteLine($"Error deleting ZIP file: {ex.Message}");
						}
					}
				}
			}
			catch (Exception ex)
			{
				// Update ExamZip with error status
				var examZip = await _unitOfWork.ExamZipRepository.GetByIdAsync(examZipId);
				if (examZip != null)
				{
					examZip.ParseStatus = ParseStatus.ERROR;
					examZip.ParseSummary = $"Fatal error: {ex.Message}\n{ex.StackTrace}";
					await _unitOfWork.SaveChangesAsync();
				}
				throw;
			}
		}

	private async Task ProcessStudentFolderAsync(string studentFolderPath, string folderName, ExamZip examZip, Exam exam)
	{
		// Use entire folder name as StudentCode (e.g., "Anhddhse170283")
		var studentCode = folderName;

		// Query Student by StudentCode
		var student = await _unitOfWork.StudentRepository.GetByStudentCodeAsync(studentCode);
		if (student == null)
		{
			throw new Exception($"Student with code '{studentCode}' not found in database");
		}

		// Query ExamStudent by ExamId and StudentId
		var examStudent = await _unitOfWork.ExamStudentRepository.GetByExamAndStudentAsync(exam.Id, student.Id);
		if (examStudent == null)
		{
			throw new Exception($"ExamStudent record not found for Student '{studentCode}' in Exam '{exam.ExamCode}'");
		}

		// Look for folder "0" inside student folder
		var zeroFolderPath = Path.Combine(studentFolderPath, "0");
		if (!Directory.Exists(zeroFolderPath))
		{
			throw new Exception("Folder '0' not found");
		}

		var s3Path = $"{exam.ExamCode}/{folderName}";
			
			// Check for .docx files directly in folder "0" first
			var existingDocxFiles = Directory.GetFiles(zeroFolderPath, "*.docx", SearchOption.TopDirectoryOnly)
				.Where(f => !Path.GetFileName(f).StartsWith("~$")) // Exclude temp Word files
				.ToList();

			// Check for solution.zip
			var solutionZipPath = Path.Combine(zeroFolderPath, "solution.zip");
			var hasSolutionZip = File.Exists(solutionZipPath);

			// Upload solution.zip to S3 if exists
			string? solutionZipS3Url = null;
			if (hasSolutionZip)
			{
				using (var zipFileStream = File.OpenRead(solutionZipPath))
				{
					solutionZipS3Url = await _s3Service.UploadFileAsync(zipFileStream, "solution.zip", s3Path);
				}
			}

			List<string> allWordFiles = new List<string>();

			// Add existing .docx files from folder 0
			allWordFiles.AddRange(existingDocxFiles);

			// Extract solution.zip if exists to find more .docx files
			if (hasSolutionZip)
			{
				var tempSolutionExtractPath = Path.Combine(Path.GetTempPath(), $"solution_{Guid.NewGuid()}");
				Directory.CreateDirectory(tempSolutionExtractPath);

				try
				{
					ZipFile.ExtractToDirectory(solutionZipPath, tempSolutionExtractPath);

					// Find all .docx files in extracted ZIP
					var wordFilesInZip = Directory.GetFiles(tempSolutionExtractPath, "*.docx", SearchOption.AllDirectories)
						.Where(f => !Path.GetFileName(f).StartsWith("~$"))
						.ToList();

					allWordFiles.AddRange(wordFilesInZip);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error extracting solution.zip: {ex.Message}");
				}
			}

		// Process all Word files found
		if (allWordFiles.Count == 0)
		{
			// No Word files found - throw error to be caught by outer try-catch
			var errorMsg = hasSolutionZip 
				? "No .docx files found in folder '0' or solution.zip" 
				: "solution.zip not found and no .docx files in folder '0'";
			throw new Exception(errorMsg);
		}

		// Process each Word file
		foreach (var wordFilePath in allWordFiles)
		{
			var fileName = Path.GetFileName(wordFilePath);

			// Upload Word file to S3
			string wordFileS3Url;
			using (var wordFileStream = File.OpenRead(wordFilePath))
			{
				wordFileS3Url = await _s3Service.UploadFileAsync(wordFileStream, fileName, s3Path);
			}

			// Extract text from Word document
			string? extractedText = null;
			string? parseMessage = null;
			DocParseStatus parseStatus;

			try
			{
				extractedText = await ExtractTextFromWordAsync(wordFilePath);
				parseStatus = DocParseStatus.OK;
				parseMessage = "Successfully parsed";
			}
			catch (Exception ex)
			{
				parseStatus = DocParseStatus.ERROR;
				parseMessage = $"Error parsing Word document: {ex.Message}";
			}

			// Create DocFile record
			var docFile = new DocFile
			{
				ExamStudentId = examStudent.Id,
				ExamZipId = examZip.Id,
				FileName = fileName,
				FilePath = wordFileS3Url,
				ParsedText = extractedText,
				ParseStatus = parseStatus,
				ParseMessage = parseMessage
			};
				await _unitOfWork.DocFileRepository.AddAsync(docFile);
				await _unitOfWork.SaveChangesAsync();

				//--------------------------------------------------------------------
				// NEW: SPLIT SOLUTION BY QUESTIONS
				//--------------------------------------------------------------------
				if (parseStatus == DocParseStatus.OK)
				{
					try 
					{
						await SplitStudentSolutionByQuestionsAsync(wordFilePath, examStudent.Id, examZip.Id, s3Path);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, $"Failed to split document {fileName} for student {studentCode}");
					}
				}
			}

			// Update ExamStudent status to PARSED
			examStudent.Status = ExamStudentStatus.PARSED;
			examStudent.Note = $"Processed {allWordFiles.Count} Word file(s)";

			await _unitOfWork.SaveChangesAsync();
		}

	private async Task SplitStudentSolutionByQuestionsAsync(string sourcePath, long examStudentId, long examZipId, string s3Path)
	{
		var questionRegex = new System.Text.RegularExpressions.Regex(@"^(Question|Câu|Q|C)\s*(\d+)\s*[:.]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
		
		int totalElements = 0;
		List<int> questionStartIndices = new List<int>();
		List<int> questionNumbers = new List<int>();

		using (WordprocessingDocument mainDoc = WordprocessingDocument.Open(sourcePath, false))
		{
			var body = mainDoc.MainDocumentPart?.Document.Body;
			if (body == null) return;

			var elements = body.ChildElements.ToList();
			totalElements = elements.Count;

			for (int i = 0; i < elements.Count; i++)
			{
				string text = elements[i].InnerText.Trim();
				var match = questionRegex.Match(text);

				if (match.Success)
				{
					questionNumbers.Add(int.Parse(match.Groups[2].Value));
					questionStartIndices.Add(i);
				}
			}
		}

		for (int q = 0; q < questionNumbers.Count; q++)
		{
			int startIdx = questionStartIndices[q];
			int endIdx = (q < questionNumbers.Count - 1) ? questionStartIndices[q + 1] - 1 : totalElements - 1;
			int qNum = questionNumbers[q];

			await SaveSplitSectionByDeletionAsync(qNum, startIdx, endIdx, sourcePath, examStudentId, examZipId, s3Path);
		}
	}

	private async Task SaveSplitSectionByDeletionAsync(int questionNum, int startIdx, int endIdx, string sourcePath, long examStudentId, long examZipId, string s3Path)
	{
		string tempPath = Path.Combine(Path.GetTempPath(), $"split_q{questionNum}_{Guid.NewGuid()}.docx");
		
		// Copy the exact document so all ImageParts and Relationships are perfectly preserved
		File.Copy(sourcePath, tempPath, true);
		
		using (WordprocessingDocument newDoc = WordprocessingDocument.Open(tempPath, true))
		{
			var body = newDoc.MainDocumentPart!.Document.Body!;
			var elements = body.ChildElements.ToList();

			for (int i = 0; i < elements.Count; i++)
			{
				if (i < startIdx || i > endIdx)
				{
					// Keep the final SectionProperties if it's the very last element in the Body to prevent corruption
					if (i == elements.Count - 1 && elements[i] is DocumentFormat.OpenXml.Wordprocessing.SectionProperties)
						continue;
						
					elements[i].Remove();
				}
			}
			newDoc.MainDocumentPart.Document.Save();
		}
		string extractedText = await ExtractTextFromWordAsync(tempPath);
		// Upload to S3
		string s3Url;
		string fileName = $"Question_{questionNum}.docx";
		using (var stream = File.OpenRead(tempPath))
		{
			s3Url = await _s3Service.UploadFileAsync(stream, fileName, s3Path);
		}

		// Save to DB
		var docFile = new DocFile
		{
			ExamStudentId = examStudentId,
			ExamZipId = examZipId,
			FileName = fileName,
			FilePath = s3Url,
			ParseStatus = DocParseStatus.OK,
			ParsedText = extractedText,
			ParseMessage = "Split section (Images preserved)",
			QuestionNumber = questionNum,
			IsEmbedded = false
		};

		await _unitOfWork.DocFileRepository.AddAsync(docFile);
		await _unitOfWork.SaveChangesAsync();

		// Cleanup
		if (File.Exists(tempPath)) File.Delete(tempPath);
	}

	public async Task<string> ExtractTextFromWordAsync(string wordFilePath)
	{
		try
		{
			var text = new StringBuilder();

			using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(wordFilePath, false))
			{
				var mainPart = wordDoc.MainDocumentPart;
				var body = mainPart?.Document?.Body;

				if (body == null || mainPart == null)
				{
					return string.Empty;
				}

				// 1. Lấy paragraph nhưng bỏ paragraph nằm trong table để tránh lặp
				foreach (var paragraph in body.Descendants<Paragraph>())
				{
					bool isInsideTable = paragraph.Ancestors<Table>().Any();
					if (isInsideTable)
						continue;

					var paragraphText = paragraph.InnerText?.Trim();
					if (!string.IsNullOrWhiteSpace(paragraphText))
					{
						text.AppendLine(paragraphText);
					}
				}

				// 2. Lấy text trong bảng
				foreach (var table in body.Descendants<Table>())
				{
					foreach (var row in table.Descendants<TableRow>())
					{
						var rowText = new List<string>();

						foreach (var cell in row.Descendants<TableCell>())
						{
							var cellText = cell.InnerText?.Trim();
							if (!string.IsNullOrWhiteSpace(cellText))
							{
								rowText.Add(cellText);
							}
						}

						if (rowText.Any())
						{
							text.AppendLine(string.Join("\t", rowText));
						}
					}
				}

				// 3. OCR ảnh trong file Word
				var processedRelIds = new HashSet<string>();

				var drawingBlips = body.Descendants<DocumentFormat.OpenXml.Drawing.Blip>()
					.Where(b => b.Embed != null)
					.Select(b => b.Embed!.Value);

				foreach (var relId in drawingBlips)
				{
					if (string.IsNullOrWhiteSpace(relId) || !processedRelIds.Add(relId))
						continue;

					try
					{
						var part = mainPart.GetPartById(relId);
						if (part is not ImagePart imagePart)
							continue;

						if (imagePart.ContentType != null &&
							(imagePart.ContentType.Contains("wmf") || imagePart.ContentType.Contains("emf")))
						{
							_logger.LogInformation("Skipping unsupported vector image type: {ContentType}", imagePart.ContentType);
							continue;
						}

						using var stream = imagePart.GetStream();
						using var ms = new MemoryStream();
						await stream.CopyToAsync(ms);

						var imageBytes = ms.ToArray();

						if (imageBytes.Length < 5 * 1024)
							continue;

						var ocrText = await _tesseractOcrService.ExtractTextFromImageBytesAsync(imageBytes, "eng");

						_logger.LogInformation(
							"OCR extracted {Length} chars from one body-referenced image in file: {WordFilePath}",
							ocrText?.Length ?? 0,
							wordFilePath);

						if (!string.IsNullOrWhiteSpace(ocrText))
						{
							text.AppendLine("[OCR_IMAGE]");
							text.AppendLine(ocrText);
						}
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "Failed to OCR one body-referenced image in Word file: {WordFilePath}", wordFilePath);
					}
				}
			}

			return text.ToString().Trim();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to extract text from Word document: {WordFilePath}", wordFilePath);
			throw;
		}
	}
}
}

