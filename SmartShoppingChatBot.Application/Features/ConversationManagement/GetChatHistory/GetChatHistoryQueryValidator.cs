using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.GetChatHistory
{
    public class GetChatHistoryQueryValidator : AbstractValidator<GetChatHistoryQuery>
    {
        public GetChatHistoryQueryValidator()
        {
            RuleFor(x => x.ExternalCustomerId)
                .NotEmpty().WithMessage("ExternalCustomerId cannot be empty")
                .MaximumLength(50).WithMessage("External customer ID cannot exceed 50 characters");

            RuleFor(x => x.Limit)
                .InclusiveBetween(1, 100).WithMessage("Limit must be between 1 and 100");
        }
    }
}
