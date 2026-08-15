using FluentValidation;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.UpdateConversationOrderStatus;

public sealed class UpdateConversationOrderStatusCommandValidator
    : AbstractValidator<UpdateConversationOrderStatusCommand>
{
    public UpdateConversationOrderStatusCommandValidator()
    {
        RuleFor(command => command.ExternalOrderId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Status)
            .Must(status => status is not ConversationOrderEventStatus.None
                and not ConversationOrderEventStatus.OrderCreated)
            .WithMessage("Status must be a valid order update status.");
    }
}
