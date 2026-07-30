using FluentValidation;
using MongoDB.Bson;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetCustomerConversationDetail;

public sealed class GetCustomerConversationDetailQueryValidator
    : AbstractValidator<GetCustomerConversationDetailQuery>
{
    public GetCustomerConversationDetailQueryValidator()
    {
        RuleFor(query => query.CustomerExternalId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("CustomerExternalId cannot be empty.")
            .MaximumLength(50).WithMessage("CustomerExternalId cannot exceed 50 characters.");

        RuleFor(query => query.ConversationId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("ConversationId cannot be empty.")
            .Must(conversationId => ObjectId.TryParse(conversationId, out _))
            .WithMessage("ConversationId must be a valid ObjectId.");

        RuleFor(query => query.Filter.LastCursor)
            .Must(lastCursor => string.IsNullOrWhiteSpace(lastCursor)
                || ObjectId.TryParse(lastCursor, out _))
            .WithMessage("LastCursor must be a valid ObjectId.");

        RuleFor(query => query.Filter.Limit)
            .InclusiveBetween(1, 100).WithMessage("Limit must be between 1 and 100.");

        RuleFor(query => query.Filter.Search)
            .MaximumLength(1024).WithMessage("Search cannot exceed 1024 characters.");

        RuleFor(query => query.Filter.SenderType)
            .Must(senderType => !senderType.HasValue
                || senderType.Value is SenderTypeEnum.Customer or SenderTypeEnum.ChatBot)
            .WithMessage("SenderType must be Customer or ChatBot.");
    }
}
