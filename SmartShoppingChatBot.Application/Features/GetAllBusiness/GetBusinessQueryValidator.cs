using FluentValidation;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.GetAllBusiness
{
    public class GetBusinessQueryValidator : AbstractValidator<GetBusinessesFilter>
    {
        public GetBusinessQueryValidator()
        {

            RuleFor(x => x.Search)
                .MaximumLength(100).WithMessage("Search term must not exceed 100 characters.");

            RuleFor(x => x.Status)
                .Must(status => status == null || Enum.IsDefined(typeof(BusinessEnums), status))
                .WithMessage("Invalid business status.");
        }
    }
}
