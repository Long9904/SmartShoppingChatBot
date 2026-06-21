using FluentValidation;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.UpdateSystemContent;

public class UpdateSystemContentCommandValidator : AbstractValidator<UpdateSystemContentCommand>
{
    public UpdateSystemContentCommandValidator()
    {
        RuleFor(command => command.Title)
            .NotEmpty().WithMessage("Title is required.");

        RuleFor(command => command.Key)
            .NotEmpty().WithMessage("Key is required.");

        RuleFor(command => command.Content)
            .NotEmpty().WithMessage("Content is required.");

        RuleFor(command => command.ContentType)
            .Must(x => Enum.TryParse<ContentType>(x, true, out _))
            .WithMessage($"ContentType must be one of the following: {string.Join(", ",Enum.GetNames<ContentType>())}.");

        RuleFor(command => command.Status)
            .Must(status => Enum.TryParse<SystemContentStatus>(status, true, out _))
            .WithMessage("Status must be Draft or Published.");
    }
}
