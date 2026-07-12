using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.BusinessMemberManagement.BusinessMemberRegistration;

public class MemberRegistrationCommandValidator : AbstractValidator<MemberRegistrationCommand>
{
    public MemberRegistrationCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.");
    }
}
