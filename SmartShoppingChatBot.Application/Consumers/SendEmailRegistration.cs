using MassTransit;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Events;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Consumers;

public class SendEmailRegistration : IConsumer<BusinessRegistrationConfirmedEvent>
{
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _templateService;

    public SendEmailRegistration(IEmailService emailService, IEmailTemplateService templateService)
    {
        _emailService = emailService;
        _templateService = templateService;
    }

    public async Task Consume(ConsumeContext<BusinessRegistrationConfirmedEvent> context)
    {
        if (context.Message.BusinessStatus == BusinessEnums.APPROVED)
        {
            var body = await _templateService.RenderEmailTemplateAsync(
                "ApproveBusinessOwner",
                new ApproveBusinessOwnerEmailModel
                {
                    BusinessName = context.Message.BusinessName,
                    OwnerEmail = context.Message.OwnerEmail,
                    OwnerName = context.Message.OwnerName,
                    VerificationToken = context.Message.BusinessId
                });
            await _emailService.SendEmailAsync(context.Message.OwnerEmail!, "Business Registration Approved", body);
        }
        else if (context.Message.BusinessStatus == BusinessEnums.REJECTED)
        {
            var body = await _templateService.RenderEmailTemplateAsync(
                "RejectBusinessOwner",
                new RejectBusinessOwnerEmailModel
                {
                    BusinessName = context.Message.BusinessName,
                    OwnerEmail = context.Message.OwnerEmail,
                    OwnerName = context.Message.OwnerName
                });
            await _emailService.SendEmailAsync(context.Message.OwnerEmail!, "Business Registration Rejected", body);
        }
    }
}
