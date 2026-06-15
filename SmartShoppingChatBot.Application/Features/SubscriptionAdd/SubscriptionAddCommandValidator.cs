using FluentValidation;
using FluentValidation.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.SubscriptionAdd
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
        }
    }
}
