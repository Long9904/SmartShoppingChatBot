using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DocumentManagement.DocumentSemanticSearch
{
    public class DocumentSemanticSearchQuery : IRequest<Result<List<DocumentResponse>>>
    {
        public DocumentSemanticSearchRequest Request { get; init; } = new();
    }
    
}
