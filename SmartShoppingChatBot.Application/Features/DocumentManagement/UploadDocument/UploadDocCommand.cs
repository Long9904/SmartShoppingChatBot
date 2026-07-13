using MediatR;
using Microsoft.AspNetCore.Http;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.DocumentManagement.UploadDocument
{
    public class UploadDocCommand : IRequest<Result<BasePaginatedList<UploadedKnowledgeDocResponse>>>
    {
        public List<IFormFile> Files { get; set; } = new();
    }
}
