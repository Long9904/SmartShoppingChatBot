using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetChatHistory
{
    public class GetChatHistoryQueryValidator : AbstractValidator<GetChatHistoryQuery>
    {
        public GetChatHistoryQueryValidator()
        {
            RuleFor(x => x.ExternalCustomerId)
                .NotEmpty().WithMessage("ExternalCustomerId cannot be empty")
                .MaximumLength(30).WithMessage("External customer ID cannot exceed 30 characters");

            RuleFor(x => x.Limit)
                .InclusiveBetween(1, 100).WithMessage("Limit must be between 1 and 100");
        }
    }
}
