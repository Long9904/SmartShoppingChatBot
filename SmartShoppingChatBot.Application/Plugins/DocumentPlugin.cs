using MediatR;
using Microsoft.SemanticKernel;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Features.DocumentManagement.DocumentSemanticSearch;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Plugins
{
    public class DocumentPlugin
    {
        private readonly IMediator mediator;

        public DocumentPlugin(IMediator mediator)
        {
            this.mediator = mediator;
        }
        [KernelFunction]
        [Description("Tìm kiếm nội dung tài liệu theo ngữ nghĩa. Must response with JSON")]
        public async Task<object> SemanticDocumentSearch(
              [Description("Thông tin tìm kiếm tài liệu")] DocumentSemanticSearchRequest request,
                [Description("Field này không được gọi")] CancellationToken cancellationToken)
        {
            // Implement the logic for semantic document search here
            // You can use the mediator to send a query or command to handle the search
            var result = await mediator.Send(
                new DocumentSemanticSearchQuery { Request = request },
                cancellationToken);
            return result;
        }

    }
}
