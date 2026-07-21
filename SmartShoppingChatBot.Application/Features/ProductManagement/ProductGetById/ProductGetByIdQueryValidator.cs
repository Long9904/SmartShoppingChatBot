using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetById;

public class ProductGetByIdQueryValidator : AbstractValidator<ProductGetByIdQuery>
{
    public ProductGetByIdQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => x.ProductId.HasValue ^ !string.IsNullOrWhiteSpace(x.ExternalId))
            .WithMessage("Exactly one product identifier is required.");

        RuleFor(x => x.ExternalId)
            .MaximumLength(100).WithMessage("ExternalId cannot exceed 100 characters.");
    }
}
