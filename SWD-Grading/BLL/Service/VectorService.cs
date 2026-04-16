using BLL.Interface;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Service
{
	public class VectorService : IVectorService
	{
		private readonly QdrantClient _qdrantClient;
		private readonly string _collectionName;
		private readonly uint _vectorSize;
		private readonly ILogger<VectorService> _logger;

	public VectorService(IConfiguration configuration, ILogger<VectorService> logger)
	{
		_logger = logger;
		var qdrantEndpoint = configuration["Qdrant:Endpoint"] ?? "http://localhost:6333";
		_collectionName = configuration["Qdrant:CollectionName"] ?? "exam_submissions";
		_vectorSize = uint.Parse(configuration["Qdrant:VectorSize"] ?? "384");

		// Parse the endpoint URL to extract host and port
		var uri = new Uri(qdrantEndpoint);
		var host = uri.Host;
		var port = uri.Port;
		var useHttps = uri.Scheme == "https";

		_qdrantClient = new QdrantClient(host: host, port: port, https: useHttps);
	}

		public async Task EnsureCollectionExistsAsync()
		{
			try
			{
				// Check if collection exists
				var collections = await _qdrantClient.ListCollectionsAsync();
				var collectionExists = collections.Contains(_collectionName);

				if (!collectionExists)
				{
					_logger.LogInformation($"Creating Qdrant collection: {_collectionName}");
					// Create collection with cosine distance
					await _qdrantClient.CreateCollectionAsync(
						collectionName: _collectionName,
						vectorsConfig: new VectorParams
						{
							Size = _vectorSize,
							Distance = Distance.Cosine
						}
					);
					_logger.LogInformation($"Collection {_collectionName} created successfully");
				}
				else
				{
					_logger.LogInformation($"Collection {_collectionName} already exists");
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to ensure Qdrant collection exists");
				throw;
			}
		}

		public async Task<float[]> GenerateEmbeddingAsync(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return new float[_vectorSize];
			}

			// Simplified embedding generation using text hashing
			// In production, use actual ONNX model inference
			return await Task.Run(() => GenerateSimpleEmbedding(text));
		}

	private float[] GenerateSimpleEmbedding(string text)
	{
	_logger.LogDebug($"Generating embedding for text with length: {text.Length}");

		// Normalize text
		text = text.ToLowerInvariant();
		var words = text.Split(new[] { ' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '?' }, 
			StringSplitOptions.RemoveEmptyEntries);

		// Create embedding vector
		var embedding = new float[_vectorSize];

	// 1. Word frequencies (unigrams) with TF weighting
		var wordFreq = new Dictionary<string, int>();
		foreach (var word in words)
		{
			if (word.Length > 2) // Skip very short words
			{
				wordFreq[word] = wordFreq.GetValueOrDefault(word, 0) + 1;
			}
		}

	// Calculate TF (Term Frequency) weights
	var totalWords = words.Length;
	var tfWeights = new Dictionary<string, float>();
		foreach (var kvp in wordFreq)
	{
		// TF = frequency / total_words
		tfWeights[kvp.Key] = (float)kvp.Value / totalWords;
	}

	// 2. Generate n-grams (bigrams and trigrams) for context
	var bigrams = new Dictionary<string, int>();
	var trigrams = new Dictionary<string, int>();
	
	for (int i = 0; i < words.Length - 1; i++)
	{
		if (words[i].Length > 2 && words[i + 1].Length > 2)
		{
			var bigram = words[i] + "_" + words[i + 1];
			bigrams[bigram] = bigrams.GetValueOrDefault(bigram, 0) + 1;
		}

		if (i < words.Length - 2 && words[i].Length > 2 && words[i + 1].Length > 2 && words[i + 2].Length > 2)
		{
			var trigram = words[i] + "_" + words[i + 1] + "_" + words[i + 2];
			trigrams[trigram] = trigrams.GetValueOrDefault(trigram, 0) + 1;
		}
	}

	// 3. Document structure features
	var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
	var sentenceCount = sentences.Length;
	var avgWordLength = words.Length > 0 ? words.Average(w => w.Length) : 0;

	// 4. Generate embedding from unigrams with IMPROVED deterministic hashing
	foreach (var kvp in tfWeights)
		{
			var wordHash = GetStableHashCode(kvp.Key);
		var weight = kvp.Value; // TF weight
		
			// Use deterministic projection for each dimension
			for (int i = 0; i < _vectorSize; i++)
			{
			// Create deterministic pseudo-random value using multiple hash functions
			var dimHash = GetDimensionHash(wordHash, i);
			var value = (float)((dimHash % 10000) / 5000.0 - 1.0); // Range: -1 to 1
			embedding[i] += value * weight * 10; // Scale up
		}
	}

	// 5. Add bigram features (give them more weight for context)
	foreach (var kvp in bigrams)
	{
		var bigramHash = GetStableHashCode(kvp.Key);
		var weight = (float)kvp.Value / totalWords;
		
		for (int i = 0; i < _vectorSize; i++)
		{
			var dimHash = GetDimensionHash(bigramHash, i + 1000);
			var value = (float)((dimHash % 10000) / 5000.0 - 1.0);
			embedding[i] += value * weight * 15; // Higher weight
			}
		}

	// 6. Add trigram features (strongest weight for phrase matching)
	foreach (var kvp in trigrams)
	{
		var trigramHash = GetStableHashCode(kvp.Key);
		var weight = (float)kvp.Value / totalWords;
		
		for (int i = 0; i < _vectorSize; i++)
		{
			var dimHash = GetDimensionHash(trigramHash, i + 2000);
			var value = (float)((dimHash % 10000) / 5000.0 - 1.0);
			embedding[i] += value * weight * 20; // Highest weight
		}
	}

	// 7. Add structural features to specific dimensions
	if (_vectorSize >= 10)
	{
		// Use last few dimensions for structural features
		embedding[_vectorSize - 3] += (float)sentenceCount / 100f;
		embedding[_vectorSize - 2] += (float)avgWordLength / 10f;
		embedding[_vectorSize - 1] += (float)words.Length / 1000f;
	}

	// 8. L2 Normalization (important for cosine similarity)
		var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
		if (magnitude > 0)
		{
			for (int i = 0; i < embedding.Length; i++)
			{
				embedding[i] /= (float)magnitude;
			}
		}

	_logger.LogDebug($"Generated embedding with {wordFreq.Count} unique words, {bigrams.Count} bigrams, {trigrams.Count} trigrams");

		return embedding;
	}

	private int GetStableHashCode(string str)
	{
		unchecked
		{
			int hash1 = 5381;
			int hash2 = hash1;

			for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
			{
				hash1 = ((hash1 << 5) + hash1) ^ str[i];
				if (i == str.Length - 1 || str[i + 1] == '\0')
					break;
				hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
			}

			return hash1 + (hash2 * 1566083941);
		}
	}

	/// <summary>
	/// Generate deterministic hash for a specific dimension
	/// This ensures same word always maps to same value in same dimension
	/// </summary>
	private int GetDimensionHash(int wordHash, int dimension)
	{
		unchecked
		{
			// Use multiple prime numbers for better distribution
			int hash = wordHash;
			hash = (hash * 16777619) ^ dimension;
			hash = (int)((hash * 2166136261) ^ (dimension >> 8));
			hash = (hash * 1000000007) ^ (wordHash >> 16);
			return Math.Abs(hash);
		}
	}

		public async Task IndexDocumentAsync(long docFileId,long examId,string studentCode,int? questionNumber,string text)
		{
			try
			{
				await EnsureCollectionExistsAsync();
			_logger.LogInformation($"[IndexDocument] Starting indexing for DocFile ID: {docFileId}, Student: {studentCode}, Exam: {examId}, Text length: {text.Length} chars");

				var embedding = await GenerateEmbeddingAsync(text);
				_logger.LogInformation("[IndexDocument] Prepared vector for DocFile {DocFileId} with dimension: {Dim}", docFileId, embedding?.Length ?? 0);

if (embedding == null || embedding.Length == 0)
{
    _logger.LogError("[IndexDocument] Generated empty embedding for DocFile {DocFileId}", docFileId);
    throw new InvalidOperationException($"Generated empty embedding for DocFile {docFileId}");
}
			_logger.LogDebug($"[IndexDocument] Generated {_vectorSize}-dimensional embedding vector for DocFile {docFileId}");

				var point = new PointStruct
{
    Id = new PointId { Num = (ulong)docFileId },
    Vectors = embedding,
    Payload =
    {
        ["docFileId"] = docFileId,
        ["examId"] = examId,
        ["studentCode"] = studentCode?.ToLower() ?? "",
        ["textLength"] = text.Length
    }
};

if (questionNumber.HasValue)
{
    point.Payload["questionNumber"] = questionNumber.Value;
}


				await _qdrantClient.UpsertAsync(
					collectionName: _collectionName,
					points: new[] { point }
				);

			_logger.LogInformation($"[IndexDocument] ✓ Successfully indexed DocFile {docFileId} (Student: {studentCode}) into Qdrant collection '{_collectionName}'");
			}
			catch (Exception ex)
			{
			_logger.LogError(ex, $"[IndexDocument] ✗ Failed to index DocFile {docFileId} for Student {studentCode}");
				throw;
			}
		}

		public async Task<bool> IsDocumentIndexedAsync(long docFileId)
		{
			try
			{
				var points = await _qdrantClient.RetrieveAsync(
					collectionName: _collectionName,
					ids: new[] { new PointId { Num = (ulong)docFileId } },
					withPayload: false,
					withVectors: false
				);

				return points.Any();
			}
			catch
			{
				return false;
			}
		}

		public async Task<long?> GetDocumentExamIdAsync(long docFileId)
		{
			try
			{
				var points = await _qdrantClient.RetrieveAsync(
					collectionName: _collectionName,
					ids: new[] { new PointId { Num = (ulong)docFileId } },
					withPayload: true,
					withVectors: false
				);

				if (!points.Any())
				{
					return null;
				}

				var point = points.First();
				return (long)point.Payload["examId"].IntegerValue;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"[GetDocumentExamId] Failed to get examId for DocFile {docFileId}");
				return null;
			}
		}

	public async Task<List<SimilarityPair>> SearchSimilarToDocumentByTextAsync(
    long docFileId,
    long examId,
    string studentCode,
    int? questionNumber,
    string text,
    float threshold)
{
    var similarPairs = new List<SimilarityPair>();

    try
    {
        _logger.LogInformation(
            "[SearchSimilar] Starting similarity search for DocFile {DocFileId} in Exam {ExamId} with threshold {Threshold:P0}",
            docFileId, examId, threshold);

        // Tạo vector query trực tiếp từ text hiện tại
        var targetVector = await GenerateEmbeddingAsync(text);

        if (targetVector == null || targetVector.Length == 0)
        {
            throw new Exception($"Failed to generate query vector for DocFile {docFileId}");
        }

        var filter = new Filter();

        // luôn cùng exam
        filter.Must.Add(new Condition
        {
            Field = new FieldCondition
            {
                Key = "examId",
                Match = new Match { Integer = examId }
            }
        });

        // nếu có questionNumber thì chỉ so cùng câu
        if (questionNumber.HasValue)
        {
            filter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "questionNumber",
                    Match = new Match { Integer = questionNumber.Value }
                }
            });
        }

        // loại cùng sinh viên
        if (!string.IsNullOrWhiteSpace(studentCode))
        {
            filter.MustNot.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "studentCode",
                   Match = new Match { Keyword = (studentCode ?? "").ToLower() }
                }
            });
        }

        // loại chính nó
        filter.MustNot.Add(new Condition
        {
            Field = new FieldCondition
            {
                Key = "docFileId",
                Match = new Match { Integer = docFileId }
            }
        });

        var searchResults = await _qdrantClient.SearchAsync(
            collectionName: _collectionName,
            vector: targetVector,
            filter: filter,
            limit: 100,
            scoreThreshold: threshold,
            payloadSelector: true
        );

        _logger.LogInformation(
            "[SearchSimilar] Qdrant returned {Count} similar documents",
            searchResults.Count);

        foreach (var result in searchResults)
        {
            var matchedDocFileId = (long)result.Payload["docFileId"].IntegerValue;

            if (matchedDocFileId == docFileId)
                continue;

            similarPairs.Add(new SimilarityPair
            {
                DocFile1Id = docFileId,
                DocFile2Id = matchedDocFileId,
                SimilarityScore = result.Score
            });
        }

        _logger.LogInformation(
            "[SearchSimilar] Completed: Found {Count} suspicious document(s) similar to DocFile {DocFileId}",
            similarPairs.Count, docFileId);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "[SearchSimilar] Failed to search similar documents for DocFile {DocFileId}",
            docFileId);
        throw;
    }

    return similarPairs;
}


		private float CosineSimilarity(float[] vector1, float[] vector2)
		{
			if (vector1.Length != vector2.Length)
				throw new ArgumentException("Vectors must have the same length");

			float dotProduct = 0;
			float magnitude1 = 0;
			float magnitude2 = 0;

			for (int i = 0; i < vector1.Length; i++)
			{
				dotProduct += vector1[i] * vector2[i];
				magnitude1 += vector1[i] * vector1[i];
				magnitude2 += vector2[i] * vector2[i];
			}

			magnitude1 = (float)Math.Sqrt(magnitude1);
			magnitude2 = (float)Math.Sqrt(magnitude2);

			if (magnitude1 == 0 || magnitude2 == 0)
				return 0;

			return dotProduct / (magnitude1 * magnitude2);
		}
		
	}
}
