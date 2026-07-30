using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DocumentManagement.DeleteDocument
{
    public class DeleteDocumentCommand : IRequest<Result<string>>
    {
        public string DocumentId { get; init; } = string.Empty;
    }
}
