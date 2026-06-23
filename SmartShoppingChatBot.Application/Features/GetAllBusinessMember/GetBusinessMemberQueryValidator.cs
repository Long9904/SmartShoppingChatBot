using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.GetAllBusinessMember
{
    public class GetBusinessMemberQueryValidator : AbstractValidator<GetBusinessMemberFilter>
    {
        public GetBusinessMemberQueryValidator()
        {
            RuleFor(x => x.UserStatus)
                .Must(x => x == null || Enum.IsDefined(typeof(Domain.Enums.UserStatus), x))
                .WithMessage("Invalid UserStatus value.");

            RuleFor(x => x.Email)
                .MaximumLength(100)
                .WithMessage("Email must not exceed 100 characters.");

            RuleFor(x => x.FullName)
                .MaximumLength(100)
                .WithMessage("FullName must not exceed 100 characters.");

            RuleFor(x => x.IsEmailVerified)
                .Must(x => x == null || x is bool)
                .WithMessage("IsEmailVerified must be a boolean value.");

            RuleFor(x => x.Gender)
                .Must(x => x == null || x is int)
                .WithMessage("Gender must be an integer value.");

        }
    }
}
