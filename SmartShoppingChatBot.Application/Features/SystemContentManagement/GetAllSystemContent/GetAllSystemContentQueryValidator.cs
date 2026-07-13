using FluentValidation;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.SystemContentManagement.GetAllSystemContent
{
    public class GetAllSystemContentQueryValidator : AbstractValidator<GetAllSystemContentFilter>
    {
        public GetAllSystemContentQueryValidator()
        {

            RuleFor(x => x.Title)
                .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

            RuleFor(x => x.Key)
                .MaximumLength(200).WithMessage("Key must not exceed 200 characters.");

            RuleFor(x => x.ContentType)
                .Must(ct => ct == null || Enum.IsDefined(typeof(ContentType), ct))
                .WithMessage("Invalid ContentType value.");

            RuleFor(x => x.Status)
                .Must(s => s == null || Enum.IsDefined(typeof(SystemContentStatus), s))
                .WithMessage("Invalid Status value.");
        }
    }
}
