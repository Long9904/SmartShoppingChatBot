using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DocumentManagement.UploadDocument
{
    public class UploadDocCommandHandler : IRequestHandler<UploadDocCommand, Result<BasePaginatedList<UploadedKnowledgeDocResponse>>>
    {
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IKnowledgeDocumentRepository _repository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;


        public UploadDocCommandHandler(
            ICloudinaryService cloudinaryService,
            IKnowledgeDocumentRepository repository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork)
        {
            _cloudinaryService = cloudinaryService;
            _repository = repository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
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
                return Result<BasePaginatedList<UploadedKnowledgeDocResponse>>.Failure(400, "No files were uploaded");
            }
            var semaphore = new SemaphoreSlim(5);
            var responseList = new List<UploadedKnowledgeDocResponse>();
            var documents = new ConcurrentBag<KnowledgeDocument>();
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
                await _repository.AddRangeAsync(documents.ToList());
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            var orderFail = responseList.OrderBy(r => r.Status == Domain.Enums.KnowledgeDocumentStatus.Failed).ToList();

            return Result<BasePaginatedList<UploadedKnowledgeDocResponse>>.Success(new BasePaginatedList<UploadedKnowledgeDocResponse>
            {
                Items = responseList,
                TotalItems = responseList.Count,
                TotalPages = 1,
                PageIndex = 1,
                PageSize = responseList.Count
            }, 201, "Upload document successfully");
        } 
       
    }
}
