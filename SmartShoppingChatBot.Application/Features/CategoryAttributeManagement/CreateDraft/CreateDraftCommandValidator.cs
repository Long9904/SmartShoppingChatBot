using FluentValidation;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.CreateDraft;

public sealed class CreateDraftCommandValidator : AbstractValidator<CreateDraftCommand>
{
    public CreateDraftCommandValidator()
    {
        RuleFor(command => command.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(250).WithMessage("Category must not exceed 250 characters.");

        RuleFor(command => command.Attributes)
            .NotNull().WithMessage("Attributes are required.")
            .NotEmpty().WithMessage("At least one attribute is required.")
            .Must(attributes => attributes is null || attributes.Select(attribute => attribute.Key?.Trim() ?? string.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() == attributes.Count)
            .WithMessage("Attribute keys must be unique.");

        RuleForEach(command => command.Attributes)
            .SetValidator(new AttributeDefinitionValidator());
    }
}

internal sealed class AttributeDefinitionValidator : AbstractValidator<AttributeDefinition>
{
    public AttributeDefinitionValidator()
    {
        RuleFor(attribute => attribute.Key)
            .NotEmpty().WithMessage("Attribute key is required.")
            .Matches("^[a-z][a-z0-9_]*$")
            .WithMessage("Attribute key must use lowercase letters, numbers, or underscores and start with a letter.");

        RuleFor(attribute => attribute.DisplayName)
            .NotEmpty().WithMessage("Attribute displayName is required.");

        RuleFor(attribute => attribute.AllowedValues)
            .Must(values => values is null || values.All(value => !string.IsNullOrWhiteSpace(value)))
            .WithMessage("Allowed values cannot contain empty values.")
            .Must(values => values is null ||
                values.Any(string.IsNullOrWhiteSpace) ||
                values.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == values.Count)
            .WithMessage("Allowed values must be unique.");

        RuleFor(attribute => attribute.AllowedValues)
            .NotEmpty()
            .When(attribute => attribute.DataType == AttributeDataType.Keyword && attribute.IsStrict)
            .WithMessage("Strict Keyword attributes require allowedValues.");

        RuleFor(attribute => attribute)
            .Must(attribute => attribute.DataType == AttributeDataType.Keyword ||
                               (attribute.AllowedValues is null || attribute.AllowedValues.Count == 0))
            .WithMessage("Only Keyword attributes can define allowedValues.");
    }
}
