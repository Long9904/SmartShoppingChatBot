using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DocumentManagement.GetAllDocument
{
    public class GetDocumentQueryHandler : IRequestHandler<GetDocumentQuery, Result<BasePaginatedList<DocumentGetResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ILogger<GetDocumentQueryHandler> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly IKnowledgeDocumentRepository _documentRepository;
        private readonly IKnowledgeEntryRepository _knowledgeEntryRepository;


        public GetDocumentQueryHandler(IUnitOfWork unitOfWork, IMapper mapper,
            ILogger<GetDocumentQueryHandler> logger, ICurrentUserService currentUserService, 
            IKnowledgeDocumentRepository documentRepository, IKnowledgeEntryRepository knowledgeEntryRepository)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _logger = logger;
            _currentUserService = currentUserService;
            _documentRepository = documentRepository;
            _knowledgeEntryRepository = knowledgeEntryRepository;
        }

        public async Task<Result<BasePaginatedList<DocumentGetResponse>>> Handle(GetDocumentQuery request, CancellationToken cancellationToken)
        {
            var business = await _currentUserService.GetBusiness();
            if (business == null || business.Data == null)
            {
                return Result<BasePaginatedList<DocumentGetResponse>>.Failure(404, "Business not found");
            }
            var query = _documentRepository.AsQueryable();
            query = query.Where(d => d.BusinessId == business.Data.Id);
            if (!string.IsNullOrEmpty(request.Filter?.FileName))
            {
                query = query.Where(d => d.FileName.Contains(request.Filter.FileName));
            }
            if (request.Filter.Status != null)
            {
                query = query.Where(d => d.Status == request.Filter.Status);
            }
            query = query.OrderByDescending(d => d.CreatedAt);
            
            var list = await _documentRepository.PaginatedListAsync(query, request.Filter?.PageIndex ?? 1,
                request.Filter?.PageSize ?? 10);

            var mappedList = new BasePaginatedList<DocumentGetResponse>
            {
                Items = MapResponse(list.Items).ToList(),
                PageIndex = list.PageIndex,
                PageSize = list.PageSize,
                TotalItems = list.TotalItems,
                TotalPages = list.TotalPages
            };

            return Result<BasePaginatedList<DocumentGetResponse>>.Success(mappedList);
        }

        private static IEnumerable<DocumentGetResponse> MapResponse(IEnumerable<KnowledgeDocument> docs)
        {
            return docs.Select(MapResponse);
        }

        private static DocumentGetResponse MapResponse(KnowledgeDocument doc)
        {
            return new DocumentGetResponse
            {
                Id = doc.Id.ToString(),
                BusinessId = doc.BusinessId.ToString(),
                ContentType = doc.ContentType,
                FileName = doc.FileName,
                PublicId = doc.PublicId,
                FileUrl = doc.FileUrl,
                SizeInBytes = doc.SizeInBytes,
                Title = doc.Title,
                Type = doc.Type,
                CreatedAt = doc.CreatedAt
            };
        }
    }
}