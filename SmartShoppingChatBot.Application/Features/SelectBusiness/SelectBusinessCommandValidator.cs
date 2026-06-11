using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.SelectBusiness
{
    public class SelectBusinessCommandValidator : AbstractValidator<SelectBusinessCommand>
    {
        public SelectBusinessCommandValidator()
        {
            RuleFor(x => x.BusinessId)
                .NotEmpty().WithMessage("BusinessId is required.");
        }
    }
}
