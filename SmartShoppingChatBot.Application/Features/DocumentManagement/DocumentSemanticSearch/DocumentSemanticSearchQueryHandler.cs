using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Qdrant.Client.Grpc;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DocumentManagement.DocumentSemanticSearch
{
    public class DocumentSemanticSearchQueryHandler : IRequestHandler<DocumentSemanticSearchQuery, Result<List<DocumentResponse>>>
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IGeminiService _geminiService;
        private readonly IQdrantService _qdrantService;
        private readonly IKnowledgeDocumentRepository _documentRepository;
        private readonly IKnowledgeEntryRepository _knowledgeEntryRepository;
        private readonly ILogger<DocumentSemanticSearchQueryHandler> _logger;

        public DocumentSemanticSearchQueryHandler(
            ICurrentUserService currentUserService,
            IGeminiService geminiService,
            IQdrantService qdrantService,
            IKnowledgeDocumentRepository documentRepository,
            IKnowledgeEntryRepository knowledgeEntryRepository,
            ILogger<DocumentSemanticSearchQueryHandler> logger)
        {
            _currentUserService = currentUserService;
            _geminiService = geminiService;
            _qdrantService = qdrantService;
            _documentRepository = documentRepository;
            _knowledgeEntryRepository = knowledgeEntryRepository;
            _logger = logger;
        }

        public async Task<Result<List<DocumentResponse>>> Handle(DocumentSemanticSearchQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Handling DocumentSemanticSearchQuery for user with query: {Query}", request.Request.Query);
            return await SearchAsync(request.Request, cancellationToken);
        }

        private async Task<Result<List<DocumentResponse>>> SearchAsync(DocumentSemanticSearchRequest request, CancellationToken ct)
        {
            var business = await _currentUserService.GetBusiness();
            if (business.Data == null || !business.IsSuccess)
            {
                return Result<List<DocumentResponse>>.Failure(
                    statusCode: 404,
                    message: "Business not found",
                    errors: null,
                    messageCode: "BUSINESS_NOT_FOUND");
            }
            if (!string.IsNullOrWhiteSpace(request.DocumentId) && !ObjectId.TryParse(request.DocumentId, out _))
            {
                return Result<List<DocumentResponse>>.Failure(
                    400,
                    "Invalid document id.");
            }
            var embedding = await _geminiService.EmbeddingsAsyncV2(request.Query, "RETRIEVAL_QUERY", ct);
            if (embedding.Data == null || !embedding.IsSuccess)
            {
                return Result<List<DocumentResponse>>.Failure(
                    statusCode: 404,
                    message: "No relevant documents found",
                    errors: null,
                    messageCode: "DOCUMENTS_NOT_FOUND");
            }
            var vector = embedding.Data.Result.Select(x => (float)x).ToArray();
            var filter = BuildFilter(business.Data.Id, request);
            var points = await _qdrantService.HybridDocumentSearchAsync(vector, vector, request.CandidateLimit, filter, ct);
            _logger.LogInformation(
                "Document search returned {PointCount} qdrant points for query: {Query}",
                points.Count,
                request.Query);

            var entries = await LoadKnowledgeEntriesAsync(points, business.Data.Id);
            _logger.LogInformation(
                "Document search loaded {EntryCount} knowledge entries from {PointCount} qdrant points for business {BusinessId}",
                entries.Count,
                points.Count,
                business.Data.Id.ToString());

            if (entries.Count == 0)
            {
                return Result<List<DocumentResponse>>.Failure(
                    statusCode: 404,
                    message: "No relevant documents found",
                    errors: null,
                    messageCode: "DOCUMENTS_NOT_FOUND");
            }
            var rerankedResult = await RerankAsync(request.Query, entries, request.TopK, ct);
            if (!rerankedResult.IsSuccess || rerankedResult.Data == null)
            {
                return Result<List<DocumentResponse>>.Failure(
                    statusCode: 404,
                    message: "No relevant documents found",
                    errors: null,
                    messageCode: "DOCUMENTS_NOT_FOUND");
            }
            return Result<List<DocumentResponse>>.Success(rerankedResult.Data);
        }

        private async Task<List<KnowledgeEntry>> LoadKnowledgeEntriesAsync(IEnumerable<ScoredPoint> scoredPoints, ObjectId businessId)
        {
            var orderIds = scoredPoints.Select(GetEntryId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
            if (orderIds.Count == 0)
            {
                return [];
            }
            var entries = await _knowledgeEntryRepository.FindAllAsync(x => orderIds.Contains(x.Id) && x.BusinessId == businessId);
            var entryById = entries.ToDictionary(x => x.Id);
            return orderIds.Where(entryById.ContainsKey).Select(id => entryById[id]).ToList();
        }

        private static ObjectId? GetEntryId(ScoredPoint point)
        {
            if (!point.Payload.TryGetValue("mongo_id", out var value))
            {
                return null;
            }

            return ObjectId.TryParse(value.StringValue, out var entryId)
                ? entryId
                : null;
        }
        private static Filter BuildFilter(ObjectId businessId, DocumentSemanticSearchRequest request)
        {
            var filter = new Filter();

            filter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "business_id",
                    Match = new Match { Keyword = businessId.ToString() }
                }
            });

            if (!string.IsNullOrWhiteSpace(request.DocumentId))
            {
                filter.Must.Add(new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "document_id",
                        Match = new Match { Keyword = request.DocumentId }
                    }
                });
            }

            return filter;
        }
        private async Task<Result<List<DocumentResponse>>> RerankAsync(string query, IReadOnlyCollection<KnowledgeEntry> entries, int topK, CancellationToken ct)
        {
            var entryById = entries.ToDictionary(x => x.Id.ToString());

            var records = entries.Select(x => new RankRecord
            {
                Id = x.Id.ToString(),
                Title = x.SectionTitle ?? x.HeadingPath ?? x.FileName,
                Content = x.Content
            });

            var reranked = await _geminiService.RerankerAsyncV2(query, records, ct);
            if (!reranked.IsSuccess || reranked.Data == null)
            {
                return Result<List<DocumentResponse>>.Failure(
                    reranked.StatusCode,
                    reranked.Message,
                    reranked.Errors,
                    reranked.MessageCode);
            }

            var responses = reranked.Data.Result
                .OrderByDescending(x => x.Score)
                .Where(x => entryById.ContainsKey(x.Id))
                .Take(topK)
                .Select(x => MapResponse(entryById[x.Id], x.Score))
                .ToList();

            return Result<List<DocumentResponse>>.Success(responses);
        }
        private static DocumentResponse MapResponse(KnowledgeEntry entry, double score)
        {
            return new DocumentResponse
            {
                EntryId = entry.Id.ToString(),
                DocumentId = entry.DocumentId.ToString(),
                FileName = entry.FileName,
                ChunkIndex = entry.ChunkIndex,
                SectionTitle = entry.SectionTitle,
                SectionSummary = entry.SectionSummary,
                HeadingPath = entry.HeadingPath,
                Content = entry.Content,
                PageStart = entry.PageStart,
                PageEnd = entry.PageEnd,
                Score = score
            };
        }
    }
}
