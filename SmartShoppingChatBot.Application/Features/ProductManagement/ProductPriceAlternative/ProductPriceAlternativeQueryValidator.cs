using FluentValidation;
using MongoDB.Bson;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductPriceAlternative;

public sealed class ProductPriceAlternativeQueryValidator : AbstractValidator<ProductPriceAlternativeQuery>
{
    public ProductPriceAlternativeQueryValidator()
    {
        RuleFor(query => query.Request)
            .NotNull().WithMessage("Request is required.");

        When(query => query.Request is not null, () =>
        {
            RuleFor(query => query.Request.ReferenceProductId)
                .NotEmpty().WithMessage("ReferenceProductId is required.")
                .Must(productId => ObjectId.TryParse(productId?.Trim(), out _))
                .WithMessage("ReferenceProductId must be a valid ObjectId.");

            RuleFor(query => query.Request.Strategy)
                .IsInEnum().WithMessage("Strategy must be DownSell or UpSell.");

            RuleFor(query => query.Request.SemanticQuery)
                .NotEmpty().WithMessage("SemanticQuery is required.")
                .MaximumLength(200).WithMessage("SemanticQuery cannot exceed 200 characters.");

            RuleFor(query => query.Request.AdditionalRequirements)
                .MaximumLength(150).WithMessage("AdditionalRequirements cannot exceed 150 characters.");

            RuleFor(query => query.Request.ExcludeProductIds)
                .NotNull().WithMessage("ExcludeProductIds is required.")
                .Must(productIds => productIds is null || productIds.Count <= 100)
                .WithMessage("ExcludeProductIds cannot contain more than 100 items.");

            RuleForEach(query => query.Request.ExcludeProductIds)
                .Must(productId => ObjectId.TryParse(productId?.Trim(), out _))
                .WithMessage("Each ExcludeProductIds item must be a valid ObjectId.");
        });
    }
}
