using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.ConfirmBusinessRegistration
{
    public class ConfirmBusinessCommand : IRequest<Result<BusinessRegistrationResponse>>
    {
        public ObjectId BusinessId { get; init; } = ObjectId.Empty;

        public bool IsApproved { get; init; }
    }
}
