using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.BusinessMemberManagement.GetAllBusinessMember
{
    public class GetBusinessMemberQueryValidator : AbstractValidator<GetBusinessMemberQuery>
    {
        public GetBusinessMemberQueryValidator()
        {
            RuleFor(x => x.Filter.UserStatus)
                .Must(x => x == null || Enum.IsDefined(typeof(Domain.Enums.UserStatus), x))
                .WithMessage("Invalid UserStatus value.");

            RuleFor(x => x.Filter.Email)
                .MaximumLength(100)
                .WithMessage("Email must not exceed 100 characters.");

            RuleFor(x => x.Filter.FullName)
                .MaximumLength(100)
                .WithMessage("FullName must not exceed 100 characters.");

            RuleFor(x => x.Filter.IsEmailVerified)
                .Must(x => x == null || x is bool)
                .WithMessage("IsEmailVerified must be a boolean value.");

            RuleFor(x => x.Filter.Gender)
                .Must(x => x == null || x is int)
                .WithMessage("Gender must be an integer value.");

        }
    }
}
