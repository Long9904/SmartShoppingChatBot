using MassTransit;
using MassTransit.Mediator;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Features.PaymentManagement.SendBillCompleted;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Consumers
{
    public class PaymentCompletedConsumer : IConsumer<PaymentCompletedEvent>
    {
        private readonly IMediator mediator;
        private readonly ILogger<PaymentCompletedConsumer> logger;

        public PaymentCompletedConsumer(IMediator mediator, ILogger<PaymentCompletedConsumer> logger)
        {
            this.mediator = mediator;
            this.logger = logger;
        }

        public async Task Consume(ConsumeContext<PaymentCompletedEvent> context)
        {
            var command = new SendBillCompletedCommand
            {
                PaymentId = context.Message.PaymentId
            };
            var result = mediator.Send(command, context.CancellationToken);
            if (!result.IsCompletedSuccessfully)
            {
                logger.LogError("Failed to send SendBillCompletedCommand for PaymentId: {PaymentId}", context.Message.PaymentId);
                return;
            }


        }
    }
}
