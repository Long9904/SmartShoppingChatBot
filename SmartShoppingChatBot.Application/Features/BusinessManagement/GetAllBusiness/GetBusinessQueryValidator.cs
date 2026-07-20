using FluentValidation;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.BusinessManagement.GetAllBusiness
{
    public class GetBusinessQueryValidator : AbstractValidator<GetBusinessesQuery>
    {
        public GetBusinessQueryValidator()
        {

            RuleFor(x => x.Filter.Search)
                .MaximumLength(100).WithMessage("Search term must not exceed 100 characters.");

            RuleFor(x => x.Filter.Status)
                .Must(status => status == null || Enum.IsDefined(typeof(BusinessEnums), status))
                .WithMessage("Invalid business status.");
        }
    }
}
