using Google.Cloud.AIPlatform.V1;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.EnumMessageCode;
using SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;

namespace SmartShoppingChatBot.Application.Features.DocumentManagement.EmbeddingDocument
{
    public class EmbeddingDocumentCommandHandler : IRequestHandler<EmbeddingDocumentCommand, Result<GeminiResponse<string>>>
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
        private readonly IBusinessQuotaRepository _businessQuotaRepository;
        private readonly IUsageQuotaLogRepository _usageQuotaLogRepository;
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
            IQdrantService qdrantService,
            IBusinessQuotaRepository businessQuotaRepository,
            IUsageQuotaLogRepository usageQuotaLogRepository)
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
            _businessQuotaRepository = businessQuotaRepository;
            _usageQuotaLogRepository = usageQuotaLogRepository;
        }

        public async Task<Result<GeminiResponse<string>>> Handle(EmbeddingDocumentCommand request, CancellationToken cancellationToken)
        {
            long totalInputToken = 0;
            long totalOutputToken = 0;
            long totalToken = 0;
            //parse id request to ObjectId
            if (!ObjectId.TryParse(request.BusinessId, out var businessId) ||
                !ObjectId.TryParse(request.DocumentId, out var documentId))
            {
                return Result<GeminiResponse<string>>.Failure(400, "Invalid business or document id.", null, DocumentMessageCode.Invalid);
            }
            //check business 
            var business = await _businessRepository.FindAsync(x =>
                x.Id == businessId &&
                x.BusinessStatus == Domain.Enums.BusinessEnums.ACTIVE);

            if (business == null)
                return Result<GeminiResponse<string>>.Failure(404, "Business not found.", null, DocumentMessageCode.NotFound);
            //check document
            var document = await _knowledgeDocumentRepository.FindAsync(x =>
                x.Id == documentId &&
                x.BusinessId == business.Id &&
                x.Status == KnowledgeDocumentStatus.Uploaded);

            if (document == null)
                return Result<GeminiResponse<string>>.Failure(404, "Uploaded document not found.", null, DocumentMessageCode.NotFound);
            //check business quota
            var businessQuota = await _businessQuotaRepository.GetCurrentBusinessQuota(business.Id);

            if (businessQuota == null)
                return Result<GeminiResponse<string>>.Failure(404, "Business quota not found", null, BusinessQuotaMessageCode.NotFound);

            var remainingTokens = businessQuota.TokenLimit - businessQuota.UsedTokens;

            if (remainingTokens < DocumentEmbeddingQuota.MaxTokensPerDocument)
            {
                document.Status = KnowledgeDocumentStatus.Failed;
                document.ErrorMessage = "Not enough token quota to embed document.";
                document.ProcessedAt = DateTimeOffset.UtcNow;
                await _knowledgeDocumentRepository.UpdateAsync(document);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result<GeminiResponse<string>>.Failure(
                    429,
                    "Not enough token quota to embed document.",
                    null,
                    BusinessQuotaMessageCode.TokenLimitExceeded);
            }
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
                    if (!embeddingText.IsSuccess || embeddingText.Data == null)
                    {
                        throw new InvalidOperationException(
                            embeddingText.Message ?? "Failed to generate section summary.");
                    }
                    if (string.IsNullOrWhiteSpace(embeddingText.Data.Result))
                        throw new InvalidOperationException($"Section summary is empty for heading: {section.HeadingPath}");
                    //llm summarize each section to get summary
                    section.SectionSummary = embeddingText.Data.Result;
                    //token usage
                    totalInputToken += embeddingText.Data.InputTokens;
                    totalOutputToken += embeddingText.Data.OutputTokens;
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
                    var technicalVector = await _geminiService.EmbeddingsAsyncV2(entry.EmbeddingText, DocumentEmbeddingTaskType);
                    if (!technicalVector.IsSuccess || technicalVector.Data == null)
                        throw new InvalidOperationException(technicalVector.Message ?? "Failed to generate document embedding.");

                    var semanticText = BuildDocumentSemanticSearchText(entry);
                    var semanticVector = await _geminiService.EmbeddingsAsyncV2(semanticText, DocumentEmbeddingTaskType);
                    if (!semanticVector.IsSuccess || semanticVector.Data == null)
                        throw new InvalidOperationException(semanticVector.Message ?? "Failed to generate semantic embedding.");
                    //create point for qdrant
                    points.Add(BuildQdrantPoint(entry, technicalVector.Data.Result, semanticVector.Data.Result));

                    //accumulate tokens
                    totalInputToken += technicalVector.Data.InputTokens + semanticVector.Data.InputTokens;
                    totalOutputToken += technicalVector.Data.OutputTokens + semanticVector.Data.OutputTokens;

                }
                totalToken = totalInputToken + (totalOutputToken * 6) + (totalInputToken / 3) + (totalOutputToken * 2);
                var newUsageQuotaLog = new UsageQuotaLog
                {
                    Id = ObjectId.GenerateNewId(),
                    BusinessId = business.Id,
                    InputTokens = totalInputToken,
                    OutputTokens = totalOutputToken,
                    BillableTokens = totalToken,
                    BusinessQuotaId = businessQuota.Id,
                    MessageUsed = 0,

                    CreatedAt = DateTimeOffset.UtcNow,
                    SourceId = document.Id,
                    SourceType = SourceTypeEnum.EmbeddingProduct,
                };
                businessQuota.UsedTokens += totalToken;
                //save entries to mongo
                await _knowledgeEntryRepository.AddRangeAsync(entries);
                //update document status
                document.ChunkCount = entries.Count;
                document.ChunkCount = entries.Count;
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
                await _businessQuotaRepository.UpdateAsync(businessQuota);
                await _usageQuotaLogRepository.AddAsync(newUsageQuotaLog);
                await _knowledgeDocumentRepository.UpdateAsync(document);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result<GeminiResponse<string>>.Success(new GeminiResponse<string> { Result = "Document embedded successfully." }, 200);
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

                return Result<GeminiResponse<string>>.Failure(400, ex.Message);
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
        private async Task<Result<GeminiResponse<string>>> GenerateSectionSummaryAsync(string headingPath, string markdownContent)
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
                var response = await _geminiService.GenerateTextAsyncV2(new GeminiRequest
                {
                    Prompt = prompt,
                    GenerationConfig = new()
                    {
                        MaxOutputTokens = 5000,
                        Temperature = 0.2
                    },
                    SystemPrompt = systemPrompt,

                });
                if (response.IsSuccess && response.Data != null)
                {
                    _logger.LogInformation("Section summary generated successfully for heading: {HeadingPath}", headingPath);
                    return Result<GeminiResponse<string>>.Success(response.Data);
                }
                _logger.LogWarning("Primary section summary generation failed for heading: {HeadingPath}. Trying fallback.", headingPath);
                var fallbackResponse = await _geminiService.GenerateTextAsyncV2(new GeminiRequest { Prompt = prompt });
                if (fallbackResponse.IsSuccess && fallbackResponse.Data != null)
                {
                    _logger.LogInformation("Fallback section summary generated successfully for heading: {HeadingPath}", headingPath);
                    return Result<GeminiResponse<string>>.Success(fallbackResponse.Data);
                }
                _logger.LogError("Both primary and fallback section summary generation failed for heading: {HeadingPath}", headingPath);
                return Result<GeminiResponse<string>>.Failure(500, "Failed to generate section summary using both attempts.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate section summary for heading: {HeadingPath}. Trying fallback.", headingPath);

                try
                {
                    var fallbackResponse = await _geminiService.GenerateTextAsyncV2(
                        new GeminiRequest
                        {
                            Prompt = prompt
                        });

                    if (fallbackResponse.IsSuccess && fallbackResponse.Data != null)
                    {
                        _logger.LogInformation(
                            "Fallback section summary generated successfully for heading: {HeadingPath}",
                            headingPath);

                        return Result<GeminiResponse<string>>.Success(
                            fallbackResponse.Data);
                    }

                    _logger.LogError(
                        "Fallback section summary generation failed for heading: {HeadingPath}",
                        headingPath);

                    return Result<GeminiResponse<string>>.Failure(
                        500,
                        "Failed to generate section summary using both attempts.");
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(
                        fallbackEx,
                        "Fallback section summary generation threw an exception for heading: {HeadingPath}",
                        headingPath);

                    return Result<GeminiResponse<string>>.Failure(
                        500,
                        "Failed to generate section summary using both attempts.");
                }
            }
        }

    }
}
