using FluentValidation;
using MongoDB.Bson;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetConversationProductComparisons;

public sealed class GetConversationProductComparisonsQueryValidator
    : AbstractValidator<GetConversationProductComparisonsQuery>
{
    public GetConversationProductComparisonsQueryValidator()
    {
        RuleFor(query => query.CustomerExternalId)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(query => query.ConversationId)
            .Must(id => ObjectId.TryParse(id, out _))
            .WithMessage("Conversation ID is invalid.");

        RuleFor(query => query.Filter.Limit)
            .InclusiveBetween(1, 100);

        RuleFor(query => query.Filter.LastCursor)
            .Must(cursor => string.IsNullOrWhiteSpace(cursor) || ObjectId.TryParse(cursor, out _))
            .WithMessage("Product comparison cursor is invalid.");
    }
}
