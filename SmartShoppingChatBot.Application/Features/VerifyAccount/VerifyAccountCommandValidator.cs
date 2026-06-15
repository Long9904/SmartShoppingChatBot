using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.VerifyAccount
{
    public class VerifyAccountCommandValidator : AbstractValidator<VerifyAccountCommand>
    {
        public VerifyAccountCommandValidator()
        {
            RuleFor(x => x.Token)
                .NotEmpty().WithMessage("Verification token is required.");

            RuleFor(x => x.Password)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(6).WithMessage("Password must be at least 6 characters long.");

            RuleFor(x => x.ConfirmPassword)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Confirm password is required.")
                .Equal(x => x.Password).WithMessage("Confirm password must match password.");

            RuleFor(x => x.PhoneNumber)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Phone number is required.")
                .Matches(@"^0(3|5|7|8|9)[0-9]{8}$").WithMessage("Phone number must be a valid Vietnamese phone number.");

            RuleFor(x => x.DateOfBirth)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithMessage("Date of birth is required.")
                .LessThan(DateTime.Now).WithMessage("Date of birth must be in the past.")
                .GreaterThan(DateTime.Now.AddYears(-120)).WithMessage("Date of birth must be within a reasonable range.");

            RuleFor(x => x.Gender)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithMessage("Gender is required.")
                .InclusiveBetween(0, 2).WithMessage("Gender must be either 0 (Female), 1 (Male), or 2 (Other).");
        }
    }
}
