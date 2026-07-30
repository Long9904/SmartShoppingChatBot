using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.DocumentManagement.GetAllDocument
{
    public class GetDocumentFilter : QueryBase
    {
        public string? FileName { get; set; }
        public KnowledgeDocumentStatus? Status { get; set; }
    }
}
