using MassTransit;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Application.Consumers
{
    public class SendEmployeeRegistrationEmail : IConsumer<EmployeeRegistrationConfirmedEvent>
    {
        private readonly IEmailService _emailService;
        private readonly IEmailTemplateService _templateService;

        public SendEmployeeRegistrationEmail(IEmailService emailService, IEmailTemplateService templateService)
        {
            _emailService = emailService;
            _templateService = templateService;
        }

        public async Task Consume(ConsumeContext<EmployeeRegistrationConfirmedEvent> context)
        {
            var body = await _templateService.RenderEmailTemplateAsync(
                "ApproveBusinessEmployee",
                new ApproveBusinessEmployeeEmailModel
                {
                    BusinessName = context.Message.BusinessName,
                    EmployeeEmail = context.Message.EmployeeEmail,
                    EmployeeName = context.Message.EmployeeName,
                    VerificationToken = context.Message.TokenVerification
                });
            await _emailService.SendEmailAsync(context.Message.EmployeeEmail!, "Confirm Employee Registration", body);
        }
    }
}
