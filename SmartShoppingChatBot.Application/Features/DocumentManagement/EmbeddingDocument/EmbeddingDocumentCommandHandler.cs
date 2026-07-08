using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;

namespace SmartShoppingChatBot.Application.Features.DocumentManagement.EmbeddingDocument
{
    public class EmbeddingDocumentCommandHandler : IRequestHandler<EmbeddingDocumentCommand, Result<string>>
    {
        private const string DocumentEmbeddingTaskType = "RETRIEVAL_DOCUMENT";

        private readonly IBusinessRepository _businessRepository;
        private readonly IKnowledgeDocumentRepository _knowledgeDocumentRepository;
        private readonly IKnowledgeEntryRepository _knowledgeEntryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IExtractFileService _extractFileService;
        private readonly IChunkService _chunkService;
        private readonly IGeminiService _geminiService;
        private readonly IQdrantService _qdrantService;
        private readonly ILogger<EmbeddingDocumentCommandHandler> _logger;

        public EmbeddingDocumentCommandHandler(
            IBusinessRepository businessRepository,
            IKnowledgeDocumentRepository knowledgeDocumentRepository,
            IKnowledgeEntryRepository knowledgeEntryRepository,
            IUnitOfWork unitOfWork,
            ILogger<EmbeddingDocumentCommandHandler> logger,
            ICloudinaryService cloudinaryService,
            IExtractFileService extractFileService,
            IChunkService chunkService,
            IGeminiService geminiService,
            IQdrantService qdrantService)
        {
            _businessRepository = businessRepository;
            _knowledgeDocumentRepository = knowledgeDocumentRepository;
            _knowledgeEntryRepository = knowledgeEntryRepository;
            _unitOfWork = unitOfWork;
            _cloudinaryService = cloudinaryService;
            _extractFileService = extractFileService;
            _chunkService = chunkService;
            _geminiService = geminiService;
            _qdrantService = qdrantService;
            _logger = logger;
        }

        public async Task<Result<string>> Handle(EmbeddingDocumentCommand request, CancellationToken cancellationToken)
        {
            if (!ObjectId.TryParse(request.BusinessId, out var businessId) ||
                !ObjectId.TryParse(request.DocumentId, out var documentId))
            {
                return Result<string>.Failure(400, "Invalid business or document id.");
            }

            var business = await _businessRepository.FindAsync(x =>
                x.Id == businessId &&
                x.BusinessStatus == Domain.Enums.BusinessEnums.ACTIVE);

            if (business == null)
                return Result<string>.Failure(404, "Business not found.");

            var document = await _knowledgeDocumentRepository.FindAsync(x =>
                x.Id == documentId &&
                x.BusinessId == business.Id &&
                x.Status == KnowledgeDocumentStatus.Uploaded);

            if (document == null)
                return Result<string>.Failure(404, "Uploaded document not found.");

            document.Status = KnowledgeDocumentStatus.Processing;
            document.ErrorMessage = null;
            document.ProcessedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            try
            {
                var downloadDoc = await _cloudinaryService.DownloadFileAsync(document.FileUrl);
                if (!downloadDoc.IsSuccess || downloadDoc.Data == null)
                    throw new InvalidOperationException(downloadDoc.Message ?? "Could not download document.");

                using var stream = downloadDoc.Data;
                var markdown = await _extractFileService.ExtractMarkdownAsync(stream, document.Type);

                if (string.IsNullOrWhiteSpace(markdown))
                    throw new InvalidOperationException("Document content is empty after extraction.");

                var sections = await _chunkService.SplitMarkdownByHeadingAsync(markdown);
                var entries = await _chunkService.ChunkSectionsAsync(
                    sections,
                    document.FileName,
                    business.Id,
                    document.Id);

                if (entries.Count == 0)
                    throw new InvalidOperationException("Document did not produce any chunks.");

                var points = new List<PointStruct>();

                foreach (var entry in entries)
                {
                    var vector = await _geminiService.EmbeddingsAsync(entry.EmbeddingText, DocumentEmbeddingTaskType);
                    if (!vector.IsSuccess || vector.Data == null)
                        throw new InvalidOperationException(vector.Message ?? "Failed to generate document embedding.");

                    points.Add(BuildQdrantPoint(entry, vector.Data));
                }

                await _qdrantService.UpsertAsync(
                    QdrantCollections.Documents,
                    points,
                    cancellationToken);

                await _knowledgeEntryRepository.AddRangeAsync(entries);

                document.ChunkCount = entries.Count;
                document.Status = KnowledgeDocumentStatus.Embedded;
                document.ErrorMessage = null;
                document.ProcessedAt = DateTimeOffset.UtcNow;

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result<string>.Success(
                    $"Embedded document {document.Id} with {entries.Count} chunks.",
                    200,
                    "Document embedded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to embed document {DocumentId}", document.Id);

                document.Status = KnowledgeDocumentStatus.Failed;
                document.ErrorMessage = ex.Message;
                document.ProcessedAt = DateTimeOffset.UtcNow;
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result<string>.Failure(400, ex.Message);
            }
        }

        private static PointStruct BuildQdrantPoint(KnowledgeEntry entry, IEnumerable<double> vector)
        {
            var point = new PointStruct
            {
                Id = new PointId { Uuid = entry.QdrantPointId },
                Vectors = new Vectors
                {
                    Vectors_ = new NamedVectors
                    {
                        Vectors =
                        {
                            [DocumentVectorNames.DocumentTechnical] = ToQdrantDenseVector(vector)
                        }
                    }
                },
                Payload =
                {
                    ["mongo_id"] = entry.Id.ToString(),
                    ["business_id"] = entry.BusinessId.ToString(),
                    ["document_id"] = entry.DocumentId.ToString(),
                    ["file_name"] = entry.FileName,
                    ["source_type"] = entry.SourceType,
                    ["chunk_index"] = entry.ChunkIndex.ToString(),
                    ["section_id"] = entry.SectionId ?? "",
                    ["section_index"] = entry.SectionIndex?.ToString() ?? "",
                    ["section_title"] = entry.SectionTitle ?? "",
                    ["section_summary"] = entry.SectionSummary ?? "",
                    ["heading_path"] = entry.HeadingPath ?? "",
                    ["content"] = TruncatePayload(entry.Content, 1000)
                }
            };

            if (entry.PageStart.HasValue)
                point.Payload["page_start"] = entry.PageStart.Value.ToString();

            if (entry.PageEnd.HasValue)
                point.Payload["page_end"] = entry.PageEnd.Value.ToString();

            return point;
        }

        private static Vector ToQdrantDenseVector(IEnumerable<double> values)
        {
            var denseVector = new DenseVector();
            denseVector.Data.Add(values.Select(x => (float)x));

            return new Vector
            {
                Dense = denseVector
            };
        }

        private static string TruncatePayload(string value, int maxLength)
        {
            if (value.Length <= maxLength)
                return value;

            return value[..maxLength];
        }
    }
}
