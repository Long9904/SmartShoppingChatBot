using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.PaymentManagement.GetPaymentByOrderCode
{
    public class GetPaymentByOrderCodeQuery : IRequest<Result<PaymentResponse>>
    {
        public long OrderCode { get; set; }

    }
}
