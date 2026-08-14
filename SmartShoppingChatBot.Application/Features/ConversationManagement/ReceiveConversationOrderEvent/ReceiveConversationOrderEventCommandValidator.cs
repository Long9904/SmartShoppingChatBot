using FluentValidation;
using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.ReceiveConversationOrderEvent;

public sealed class ReceiveConversationOrderEventCommandValidator
    : AbstractValidator<ReceiveConversationOrderEventCommand>
{
    public ReceiveConversationOrderEventCommandValidator()
    {
        RuleFor(command => command.ConversationId)
            .Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("Conversation ID is invalid.");

        RuleFor(command => command.Event.ExternalOrderId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Event.Status)
            .NotEqual(ConversationOrderEventStatus.None)
            .WithMessage("Order event status is required.");

        RuleFor(command => command.Event.Amount)
            .GreaterThanOrEqualTo(0);

        RuleForEach(command => command.Event.Products).ChildRules(product =>
        {
            product.RuleFor(item => item.ExternalProductId)
                .NotEmpty()
                .MaximumLength(100);
            product.RuleFor(item => item.ProductName)
                .MaximumLength(300);
            product.RuleFor(item => item.Price)
                .GreaterThanOrEqualTo(0);
            product.RuleFor(item => item.Quantity)
                .GreaterThan(0)
                .When(item => item.Quantity.HasValue);
        });
    }
}
