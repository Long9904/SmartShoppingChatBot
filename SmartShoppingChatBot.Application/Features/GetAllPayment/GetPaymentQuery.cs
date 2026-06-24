using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.GetAllPayment
{
    public class GetPaymentQuery : IRequest<Result<BasePaginatedList<PaymentResponse>>>
    {
        public GetPaymentFilter Filter { get; set; } = new();
    }
}
