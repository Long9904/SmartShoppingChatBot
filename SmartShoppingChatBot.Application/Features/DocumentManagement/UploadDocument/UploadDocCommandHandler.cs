using MassTransit;
using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.EnumMessageCode;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using System.Collections.Concurrent;

namespace SmartShoppingChatBot.Application.Features.DocumentManagement.UploadDocument
{
    public class UploadDocCommandHandler : IRequestHandler<UploadDocCommand, Result<BasePaginatedList<UploadedKnowledgeDocResponse>>>
    {
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IKnowledgeDocumentRepository _repository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPublishEndpoint _publisher;

        public UploadDocCommandHandler(
            ICloudinaryService cloudinaryService,
            IKnowledgeDocumentRepository repository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork,
            IPublishEndpoint publisher)
        {
            _cloudinaryService = cloudinaryService;
            _repository = repository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
            _publisher = publisher;
        }


        public async Task<Result<BasePaginatedList<UploadedKnowledgeDocResponse>>> Handle(UploadDocCommand request, CancellationToken cancellationToken)
        {
            var currentBusiness = await _currentUserService.GetBusiness();
            if (!currentBusiness.IsSuccess || currentBusiness.Data == null)
            {
                return Result<BasePaginatedList<UploadedKnowledgeDocResponse>>.Failure(
                    currentBusiness.StatusCode,
                    currentBusiness.Message,
                    currentBusiness.Errors);
            }

            if (request.Files == null || request.Files.Count == 0)
            {
                return Result<BasePaginatedList<UploadedKnowledgeDocResponse>>.Failure(500, "No files were uploaded", null, DocumentMessageCode.UploadFailed);
            }
            // Limit the number of concurrent uploads to 5
            var semaphore = new SemaphoreSlim(5);
            var responseList = new ConcurrentBag<UploadedKnowledgeDocResponse>();
            var documents = new ConcurrentBag<KnowledgeDocument>();
            // Process each file upload concurrently
            var task = request.Files.Select(async file =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var uploadResult = await _cloudinaryService.UploadFileAsync(file, currentBusiness.Data.Id.ToString(), "knowledge-documents");
                    if (!uploadResult.IsSuccess || uploadResult.Data == null)
                    {
                        responseList.Add(new UploadedKnowledgeDocResponse
                        {
                            FileName = file.FileName,
                            Status = KnowledgeDocumentStatus.Failed,
                            ErrorMessage = uploadResult.Message
                        });
                        return;
                    }
                    var uploadedFileResponse = uploadResult.Data;
                    if (uploadedFileResponse == null)
                    {
                        responseList.Add(new UploadedKnowledgeDocResponse
                        {
                            FileName = file.FileName,
                            Status = KnowledgeDocumentStatus.Failed,
                            ErrorMessage = "File upload failed: No response from Cloudinary"
                        });
                        return;
                    }
                    var knowledgeDocument = new KnowledgeDocument
                    {
                        Id = ObjectId.GenerateNewId(),
                        BusinessId = currentBusiness.Data.Id,
                        Title = Path.GetFileNameWithoutExtension(file.FileName),
                        FileName = uploadedFileResponse.FileName,
                        PublicId = uploadedFileResponse.PublicId,
                        ContentType = uploadedFileResponse.ContentType,
                        FileUrl = uploadedFileResponse.FileUrl,
                        Type = Path.GetExtension(file.FileName).TrimStart('.'),
                        SizeInBytes = uploadedFileResponse.SizeInBytes,
                        ChunkCount = 0,
                        Status = Domain.Enums.KnowledgeDocumentStatus.Uploaded,
                        CreatedAt = DateTimeOffset.UtcNow
                    };
                    documents.Add(knowledgeDocument);
                    responseList.Add(new UploadedKnowledgeDocResponse
                    {
                        DocumentId = knowledgeDocument.Id.ToString(),
                        FileName = knowledgeDocument.FileName,
                        Status = knowledgeDocument.Status,
                        ErrorMessage = null
                    });
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(task);

            if (documents.Count > 0)
            {
                var documentsList = documents.ToList();
                await _repository.AddRangeAsync(documentsList);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                // Publish events for each uploaded document
                foreach (var doc in documentsList)
                {
                    await _publisher.Publish(new DocumentUploadedEvent
                    {
                        DocumentId = doc.Id.ToString(),
                        BusinessId = doc.BusinessId.ToString(),
                    }, cancellationToken);
                }
            }
            var items = responseList.OrderBy(r => r.Status == KnowledgeDocumentStatus.Failed).ToList();

            return Result<BasePaginatedList<UploadedKnowledgeDocResponse>>.Success(new BasePaginatedList<UploadedKnowledgeDocResponse>
            {
                Items = items,
                TotalItems = items.Count,
                TotalPages = 1,
                PageIndex = 1,
                PageSize = items.Count
            }, 201, "Upload document successfully");
        }

    }
}
