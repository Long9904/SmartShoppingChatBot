using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
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

        public Task<Result<List<DocumentResponse>>> Handle(DocumentSemanticSearchQuery request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
