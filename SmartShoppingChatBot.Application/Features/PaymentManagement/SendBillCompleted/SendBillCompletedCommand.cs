using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;

namespace SmartShoppingChatBot.Application.Features.PaymentManagement.SendBillCompleted
{
    public class SendBillCompletedCommand : IRequest<Result<string>>
    {
        public string PaymentId { get; set; } = string.Empty;
    }
}
