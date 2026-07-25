using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.PaymentManagement.GetAllPaymentByUser
{
    public class GetPaymentByUserQuery : IRequest<Result<BasePaginatedList<PaymentResponse>>>
    {
        public GetPaymentFilterByUser Filter { get; set; } = new();
    }
}
