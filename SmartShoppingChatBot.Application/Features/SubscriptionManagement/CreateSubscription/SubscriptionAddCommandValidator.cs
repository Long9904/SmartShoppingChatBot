using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.SubscriptionManagement.CreateSubscription
{
    public class SubscriptionAddCommandValidator : AbstractValidator<SubscriptionAddCommand>
    {
        public SubscriptionAddCommandValidator()
        {
            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Subscription name is required.")
                .MaximumLength(100).WithMessage("Subscription name must not exceed 100 characters.");
            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Subscription description is required.")
                .MaximumLength(500).WithMessage("Subscription description must not exceed 500 characters.");
            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("Price must be greater than 0.");
            RuleFor(x => x.Duration)
                .GreaterThan(0).WithMessage("Duration must be greater than 0.");
            RuleFor(x => x.TokenLimit)
                .GreaterThan(0).WithMessage("Token limit must be greater than 0.");
            RuleFor(x => x.MessageLimit)
                .GreaterThan(0).WithMessage("Message limit must be greater than 0.");
            RuleFor(x => x.MaxProductAllowed)
                .GreaterThan(0).WithMessage("Max product allowed must be greater than 0.");
            RuleFor(x => x.MaxDocmentAllowed)
                .GreaterThan(0).WithMessage("Max document allowed must be greater than 0.");
        }
    }
}
