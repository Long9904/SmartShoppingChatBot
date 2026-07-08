using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DocumentManagement.EmbeddingDocument
{
    public class EmbeddingDocumentCommandHandler : IRequestHandler<EmbeddingDocumentCommand, Result<string>>
    {
        private readonly IKnowledgeEntryRepository _entryRepository;
        private readonly IBusinessRepository _businessRepository;
        private readonly IKnowledgeDocumentRepository _knowledgeDocumentRepository;
        private readonly IKnowledgeEntryRepository _knowledgeEntryRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IExtractFileService _extractFileService;
        private readonly IChunkService _chunkService;
        private readonly ILogger<EmbeddingDocumentCommand> _logger;


        public EmbeddingDocumentCommandHandler(IKnowledgeEntryRepository entryRepository, IBusinessRepository businessRepository,
            IKnowledgeDocumentRepository knowledgeDocumentRepository, IKnowledgeEntryRepository knowledgeEntryRepository, IUnitOfWork unitOfWork
            , ILogger<EmbeddingDocumentCommand> logger, ICloudinaryService cloudinaryService, IExtractFileService extractFileService, IChunkService chunkService)
        {
            _entryRepository = entryRepository;
            _businessRepository = businessRepository;
            _knowledgeDocumentRepository = knowledgeDocumentRepository;
            _knowledgeEntryRepository = knowledgeEntryRepository;
            _unitOfWork = unitOfWork;
            _cloudinaryService = cloudinaryService;
            _extractFileService = extractFileService;
            _chunkService = chunkService;
            _logger = logger;
        }

        public async Task<Result<string>> Handle(EmbeddingDocumentCommand request, CancellationToken cancellationToken)
        {
            var businnes = await _businessRepository.FindAsync(x => x.Id == ObjectId.Parse(request.BusinessId)
            && x.BusinessStatus == Domain.Enums.BusinessEnums.ACTIVE);
            if (businnes == null)
            {
                return Result<string>.Failure(404, "Item not found");
            }
            var document = await _knowledgeDocumentRepository.FindAsync(x => x.Id == ObjectId.Parse(request.DocumentId)
            && x.Status == KnowledgeDocumentStatus.Uploaded);
            if (document == null)
            {
                return Result<string>.Failure(404, "Item not found");
            }
            document.Status = KnowledgeDocumentStatus.Processing;
            document.ProcessedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            //
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                // 1. Download Cloudinary temp file
                var downloadDoc = await _cloudinaryService.DownloadFileAsync(document.FileUrl);
                if (!downloadDoc.IsSuccess)
                {
                    return Result<string>.Failure(404, downloadDoc.Message);
                }
                // 2. Extract text
                using var stream = downloadDoc.Data;
                var extractedText = await _extractFileService.ExtractDocxAsync(stream);
                // 3. Chunk
                var chunks = await _chunkService.ChunkMarkdownAsync(extractedText, document.FileName, businnes.Id, document.Id);

                var entries = chunks.Select(x => new KnowledgeEntry
                {
                    Id = ObjectId.GenerateNewId(),

                    BusinessId = businnes.Id,
                    DocumentId = document.Id,

                    QdrantPointId = Guid.NewGuid().ToString(),

                    ChunkIndex = x.ChunkIndex,

                    Content = x.Content,
                    ContextualContent = x.ContextualContent,
                    EmbeddingText = x.EmbeddingText,

                    HeadingPath = x.HeadingPath,
                    TokenCount = x.TokenCount,

                    FileName = document.FileName,
                    SourceType = "knowledge_document",

                    CreatedAt = DateTime.UtcNow
                }).ToList();
                await _knowledgeEntryRepository.AddRangeAsync(entries);
                await _unitOfWork.SaveChangesAsync();
                // 4. Contextual Retrieval

                // 5. Embedding
                // 6. Upsert Qdrant
                // 7. Delete temp file
                document.Status = KnowledgeDocumentStatus.Embedded;
                document.ProcessedAt = DateTimeOffset.Now;
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                return null;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollBackAsync(cancellationToken);
                return Result<string>.Failure(400, ex.Message);
            }
        }
    }
}