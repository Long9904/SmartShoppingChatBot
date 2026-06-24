using MediatR;
using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.UserManagement.GetAllUser
{
    public class GetAllUserQuery : QueryBase, IRequest<Result<BasePaginatedList<object>>>
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }

        public bool? IsEmailVerified { get; set; }

        public int? Gender { get; set; }

        public UserStatus? UserStatus { get; set; }

        public string? OrderBy { get; set; }
    }
}
