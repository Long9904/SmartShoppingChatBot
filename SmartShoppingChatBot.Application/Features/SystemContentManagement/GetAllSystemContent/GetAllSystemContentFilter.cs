using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.SystemContentManagement.GetAllSystemContent
{
    public class GetAllSystemContentFilter : QueryBase
    {
        public string? Title { get; set; }

        public string? Key { get; set; }

        public ContentType? ContentType { get; set; }

        public SystemContentStatus? Status { get; set; }

        public string? OrderBy { get; set; } = "CreatedAt desc";
    }
}
