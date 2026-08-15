using FluentValidation;
using MongoDB.Bson;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.RegisterConversationOrder;

public sealed class RegisterConversationOrderCommandValidator
    : AbstractValidator<RegisterConversationOrderCommand>
{
    public RegisterConversationOrderCommandValidator()
    {
        RuleFor(command => command.ConversationId)
            .Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("Conversation ID is invalid.");

        RuleFor(command => command.Order.ExternalOrderId)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Order.Amount)
            .GreaterThanOrEqualTo(0);

        RuleForEach(command => command.Order.Products).ChildRules(product =>
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
