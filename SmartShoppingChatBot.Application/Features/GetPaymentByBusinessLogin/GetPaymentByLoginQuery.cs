using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.GetPaymentByBusinessLogin
{
    public class GetPaymentByLoginQuery : IRequest<Result<PaymentResponse>>
    {
        public long OrderCode { get; set; }

    }
}
