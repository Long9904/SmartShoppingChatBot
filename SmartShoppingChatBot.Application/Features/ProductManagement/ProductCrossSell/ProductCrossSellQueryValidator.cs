using FluentValidation;
using MongoDB.Bson;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductCrossSell;

public sealed class ProductCrossSellQueryValidator : AbstractValidator<ProductCrossSellQuery>
{
    public ProductCrossSellQueryValidator()
    {
        RuleFor(query => query.Request)
            .NotNull().WithMessage("Request is required.");

        When(query => query.Request is not null, () =>
        {
            RuleFor(query => query.Request.ReferenceProductId)
                .NotEmpty().WithMessage("ReferenceProductId is required.")
                .Must(productId => ObjectId.TryParse(productId?.Trim(), out _))
                .WithMessage("ReferenceProductId must be a valid ObjectId.");

            RuleFor(query => query.Request.SemanticQuery)
                .NotEmpty().WithMessage("SemanticQuery is required.")
                .MaximumLength(500).WithMessage("SemanticQuery cannot exceed 500 characters.");

            RuleFor(query => query.Request.AccessoryNeed)
                .MaximumLength(300).WithMessage("AccessoryNeed cannot exceed 300 characters.");

            RuleFor(query => query.Request.MaxPrice)
                .GreaterThanOrEqualTo(0).WithMessage("MaxPrice must be greater than or equal to 0.")
                .When(query => query.Request.MaxPrice.HasValue);

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
