using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.CustomerGetConversations
{
    public class CustomerGetConversationsQueryValidator : AbstractValidator<CustomerGetConversationsQuery>
    {
        public CustomerGetConversationsQueryValidator()
        {
            RuleFor(x => x.ExternalCustomerId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("ExternalCustomerId cannot be empty")
                .MaximumLength(30).WithMessage("External customer ID cannot exceed 30 characters");

            RuleFor(x => x.PageIndex)
                .GreaterThan(0).WithMessage("PageIndex must be greater than 0");

            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage("PageSize must be greater than 0");
        }
    }
}
