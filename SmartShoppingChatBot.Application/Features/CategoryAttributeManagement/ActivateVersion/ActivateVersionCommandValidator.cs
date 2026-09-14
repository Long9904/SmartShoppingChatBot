using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.ActivateVersion;

public sealed class ActivateVersionCommandValidator : AbstractValidator<ActivateVersionCommand>
{
    public ActivateVersionCommandValidator()
    {
        RuleFor(command => command.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(250).WithMessage("Category must not exceed 250 characters.");

        RuleFor(command => command.Version)
            .GreaterThan(0).WithMessage("Version must be greater than zero.");
    }
}
