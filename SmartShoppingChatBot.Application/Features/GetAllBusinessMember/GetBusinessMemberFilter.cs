using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.GetAllBusinessMember
{
    public class GetBusinessMemberFilter : QueryBase
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }

        public bool? IsEmailVerified { get; set; }

        public int? Gender { get; set; }

        public UserStatus? UserStatus { get; set; }

        public string? OrderBy { get; set; }

    }
}
