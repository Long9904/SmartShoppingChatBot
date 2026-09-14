using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.UpdateAttributeDefinition;

public sealed class UpdateAttributeDefinitionCommandValidator
    : AbstractValidator<UpdateAttributeDefinitionCommand>
{
    public UpdateAttributeDefinitionCommandValidator()
    {
        RuleFor(command => command.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(250).WithMessage("Category must not exceed 250 characters.");

        RuleFor(command => command.AttributeKey)
            .NotEmpty().WithMessage("Attribute key is required.")
            .Matches("^[a-z][a-z0-9_]*$")
            .WithMessage("Attribute key must use lowercase letters, numbers, or underscores and start with a letter.");

        RuleFor(command => command.DisplayName)
            .NotEmpty().WithMessage("Attribute displayName is required.");

        RuleFor(command => command.AllowedValues)
            .Must(values => values is null || values.All(value => !string.IsNullOrWhiteSpace(value)))
            .WithMessage("Allowed values cannot contain empty values.")
            .Must(values => values is null ||
                values.Any(string.IsNullOrWhiteSpace) ||
                values.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == values.Count)
            .WithMessage("Allowed values must be unique.");
    }
}
