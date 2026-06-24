using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.ConfirmBusinessRegistration
{
    public class ConfirmBusinessCommandValidator : AbstractValidator<ConfirmBusinessCommand>
    {
        public ConfirmBusinessCommandValidator()
        {
            RuleFor(x => x.BusinessId)
                .NotEmpty().WithMessage("Business ID is required.");

            RuleFor(x => x.IsApproved)
                .NotNull().WithMessage("Approval status is required.");

        }
    }
}
