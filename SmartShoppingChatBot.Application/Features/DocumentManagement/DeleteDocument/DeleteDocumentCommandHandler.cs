using DocumentFormat.OpenXml.Office2013.PowerPoint;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Domain.QdrantConfig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DocumentManagement.DeleteDocument
{
    public class DeleteDocumentCommandHandler : IRequestHandler<DeleteDocumentCommand, Result<string>>
    {
        private readonly ILogger<DeleteDocumentCommandHandler> _logger;
        private readonly IKnowledgeDocumentRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IKnowledgeEntryRepository _entryRepository;
        private readonly IQdrantService _qdrantService;

        public DeleteDocumentCommandHandler(ILogger<DeleteDocumentCommandHandler> logger, 
            IKnowledgeDocumentRepository repository, IUnitOfWork unitOfWork, IKnowledgeEntryRepository entryRepository, IQdrantService qdrantService)
        {
            _logger = logger;
            _repository = repository;
            _unitOfWork = unitOfWork;
            _entryRepository = entryRepository;
            _qdrantService = qdrantService;
        }



        public async Task<Result<string>> Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
        {
            if (!ObjectId.TryParse(request.DocumentId, out var id))
            {
                return Result<string>.Failure(400, "Invalid document ID format.");
            }
            // Check if the document exists and is not already deleted
            var document = await _repository.FindAsync(x => x.Id == id && x.Status != Domain.Enums.KnowledgeDocumentStatus.Deleted);
            if (document == null)
            {
                return Result<string>.Failure(404, "Document not found.");
            }
            // Retrieve all entries associated with the document
            var entries = await _entryRepository.FindAllAsync(x => x.DocumentId == id);
            var pointIds = entries
                .Select(x => Guid.TryParse(x.QdrantPointId, out var guid) ? guid : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToList();
            if(pointIds.Count > 0)
            {
               await _qdrantService.DeletePointsAsync(QdrantCollections.Documents, pointIds, cancellationToken);
            }

            // Delete the document and its associated entries
            document.Status = Domain.Enums.KnowledgeDocumentStatus.Deleted;
            document.ProcessedAt = DateTime.UtcNow;
            await _repository.UpdateAsync(document);
            foreach (var entry in entries)
            {
                await _entryRepository.DeleteAsync(entry.Id);
            }
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result<string>.Success("Document deleted successfully.");
        }
    }
}
