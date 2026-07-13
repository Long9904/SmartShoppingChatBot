using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.EnumMessageCode;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;
using static System.Collections.Specialized.BitVector32;

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
            //parse id request to ObjectId
            if (!ObjectId.TryParse(request.BusinessId, out var businessId) ||
                !ObjectId.TryParse(request.DocumentId, out var documentId))
            {
                return Result<string>.Failure(400, "Invalid business or document id.",null,DocumentMessageCode.Invalid);
            }
            //check business 
            var business = await _businessRepository.FindAsync(x =>
                x.Id == businessId &&
                x.BusinessStatus == Domain.Enums.BusinessEnums.ACTIVE);

            if (business == null)
                return Result<string>.Failure(404, "Business not found.",null,DocumentMessageCode.NotFound);
            //check document
            var document = await _knowledgeDocumentRepository.FindAsync(x =>
                x.Id == documentId &&
                x.BusinessId == business.Id &&
                x.Status == KnowledgeDocumentStatus.Uploaded);

            if (document == null)
                return Result<string>.Failure(404, "Uploaded document not found.",null,DocumentMessageCode.NotFound);
            //update document processing 
            document.Status = KnowledgeDocumentStatus.Processing;
            document.ErrorMessage = null;
            document.ProcessedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            try
            {
                //download doc form cloudinary
                var downloadDoc = await _cloudinaryService.DownloadFileAsync(document.FileUrl);
                if (!downloadDoc.IsSuccess || downloadDoc.Data == null)
                    throw new InvalidOperationException(downloadDoc.Message ?? "Could not download document.");
                //excute extract markdown from doc
                using var stream = downloadDoc.Data;
                var markdown = await _extractFileService.ExtractMarkdownAsync(stream, document.Type);

                if (string.IsNullOrWhiteSpace(markdown))
                    throw new InvalidOperationException("Document content is empty after extraction.");
                //cut markdown by heading(create object of this heading)
                var sections = await _chunkService.SplitMarkdownByHeadingAsync(markdown);
                
                foreach (var section in sections)
                {
                    var embeddingText = await GenerateSectionSummaryAsync(section.HeadingPath, section.MarkdownContent);
                    if (string.IsNullOrWhiteSpace(embeddingText.Data))
                        continue;
                    //llm summarize each section to get summary
                    section.SectionSummary = embeddingText.Data;
                }
                //cut section to chunk
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
                    //generate embedding for each chunk
                    var technicalVector = await _geminiService.EmbeddingsAsync(entry.EmbeddingText, DocumentEmbeddingTaskType);
                    if (!technicalVector.IsSuccess || technicalVector.Data == null)
                        throw new InvalidOperationException(technicalVector.Message ?? "Failed to generate document embedding.");

                    var semanticText = BuildDocumentSemanticSearchText(entry);
                    var semanticVector = await _geminiService.EmbeddingsAsync(semanticText,DocumentEmbeddingTaskType);
                    if(!semanticVector.IsSuccess || semanticVector.Data == null)
                        throw new InvalidOperationException(semanticVector.Message ?? "Failed to generate semantic embedding.");
                    //create point for qdrant
                    points.Add(BuildQdrantPoint(entry, technicalVector.Data, semanticVector.Data));
                }
           
                //save entries to mongo
                await _knowledgeEntryRepository.AddRangeAsync(entries);
                //update document status
                document.ChunkCount = entries.Count;
                document.Status = KnowledgeDocumentStatus.Processing;
                document.ErrorMessage = null;
                document.ProcessedAt = DateTimeOffset.UtcNow;
                await _knowledgeDocumentRepository.UpdateAsync(document);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                //upsert points to qdrant collection
                await _qdrantService.UpsertAsync(
                    QdrantCollections.Documents,
                    points,
                    cancellationToken);

                //update document status to embedded
                document.Status = KnowledgeDocumentStatus.Embedded;
                document.ProcessedAt = DateTimeOffset.UtcNow;
                await _knowledgeDocumentRepository.UpdateAsync(document);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result<string>.Success(
                    $"Embedded document {document.Id} with {entries.Count} chunks.",
                    200,
                    "Document embedded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to embed document {DocumentId}", document.Id);
                await _knowledgeEntryRepository.DeleteAsync(document.Id);
                document.Status = KnowledgeDocumentStatus.Failed;
                document.ErrorMessage = ex.Message;
                document.ProcessedAt = DateTimeOffset.UtcNow;
                await _knowledgeDocumentRepository.UpdateAsync(document);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result<string>.Failure(400, ex.Message);
            }
        }

        private static PointStruct BuildQdrantPoint(KnowledgeEntry entry, IEnumerable<double> vector, IEnumerable<double> semanticVector)
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
                            [DocumentVectorNames.DocumentTechnical] = ToQdrantDenseVector(vector),
                            [DocumentVectorNames.SemanticSearch] = ToQdrantDenseVector(semanticVector)
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
        //convert double to vector
        private static Vector ToQdrantDenseVector(IEnumerable<double> values)
        {
            var denseVector = new DenseVector();
            denseVector.Data.Add(values.Select(x => (float)x));

            return new Vector
            {
                Dense = denseVector
            };
        }
        // Truncate the payload value to a maximum length
        private static string TruncatePayload(string value, int maxLength)
        {
            if (value.Length <= maxLength)
                return value;

            return value[..maxLength];
        }
        private static string BuildDocumentSemanticSearchText(KnowledgeEntry entry)
        {
            return $"""
                    Tài liệu: {entry.FileName}
                    Mục: {entry.HeadingPath}
                    Tóm tắt: {entry.SectionSummary}

                    Nội dung:
                    {entry.Content}
                    """;
        }
        private async Task<Result<string>> GenerateSectionSummaryAsync(string headingPath, string markdownContent)
        {
            var systemPrompt = await File.ReadAllTextAsync("prompts/SectionSummary.md");

            var prompt =
                $"""
                {systemPrompt}

                SECTION_HEADING_PATH:
                {headingPath}

                SECTION_CONTENT_BEGIN
                {markdownContent}
                SECTION_CONTENT_END
                """;
            try
            {
                var response = await _geminiService.GenerateTextAsync(prompt, 5000, 0.2);
                if (response.IsSuccess)
                {
                    _logger.LogInformation($"Data generated using QwenService: {response.Data}");
                    return Result<string>.Success(response.Data);
                }
                else
                {
                    var fallbackResponse = await _geminiService.GenerateTextAsync(prompt, 5000, 0.2);
                    return Result<string>.Success(fallbackResponse.Data);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate semantic search text using QwenService. Falling back to GeminiService.");

                var response = await _geminiService.GenerateTextAsync(prompt, 5000, 0.2);
                if (!response.IsSuccess)
                {
                    _logger.LogError("GeminiService also failed to generate semantic search text.");
                    return Result<string>.Failure(500, "Failed to generate semantic search text using both QwenService and GeminiService.");
                }

                return Result<string>.Success(response.Data);
            }
        }

    }
}
