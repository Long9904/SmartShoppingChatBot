using FluentValidation;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.SystemContentManagement.GetAllSystemContent
{
    public class GetAllSystemContentQueryValidator : AbstractValidator<GetAllSystemContentQuery>
    {
        public GetAllSystemContentQueryValidator()
        {

            RuleFor(x => x.Filter.Title)
                .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

            RuleFor(x => x.Filter.Key)
                .MaximumLength(200).WithMessage("Key must not exceed 200 characters.");

            RuleFor(x => x.Filter.ContentType)
                .Must(ct => ct == null || Enum.IsDefined(typeof(ContentType), ct))
                .WithMessage("Invalid ContentType value.");

            RuleFor(x => x.Filter.Status)
                .Must(s => s == null || Enum.IsDefined(typeof(SystemContentStatus), s))
                .WithMessage("Invalid Status value.");
        }
    }
}
