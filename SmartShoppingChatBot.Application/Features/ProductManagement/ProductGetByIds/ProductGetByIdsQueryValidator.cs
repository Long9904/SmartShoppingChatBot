using FluentValidation;
using MongoDB.Bson;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetByIds;

public sealed class ProductGetByIdsQueryValidator : AbstractValidator<ProductGetByIdsQuery>
{
    private const int MaximumProductCount = 20;

    public ProductGetByIdsQueryValidator()
    {
        RuleFor(query => query.ProductIds)
            .NotEmpty().WithMessage("At least one product ID is required.")
            .Must(productIds => productIds.Count <= MaximumProductCount)
            .WithMessage($"A maximum of {MaximumProductCount} product IDs is allowed.");

        RuleForEach(query => query.ProductIds)
            .NotEmpty().WithMessage("Product ID cannot be empty.")
            .Must(productId => ObjectId.TryParse(productId?.Trim(), out _))
            .WithMessage("Product ID must be a valid ObjectId.");
    }
}
