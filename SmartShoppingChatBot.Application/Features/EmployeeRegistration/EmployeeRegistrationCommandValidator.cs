using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.EmployeeRegistration;

public class EmployeeRegistrationCommandValidator : AbstractValidator<EmployeeRegistrationCommand>
{
    public EmployeeRegistrationCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.");
    }
}
