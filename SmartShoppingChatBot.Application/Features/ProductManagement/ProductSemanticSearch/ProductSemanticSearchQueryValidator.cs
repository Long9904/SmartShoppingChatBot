using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductSemanticSearch
{
    public class ProductSemanticSearchQueryValidator : AbstractValidator<ProductSemanticSearchQuery>
    {
        public ProductSemanticSearchQueryValidator()
        {
            RuleFor(x => x.Request.SemanticQuery)
                .NotEmpty().WithMessage("Query is required.")
                .MaximumLength(500).WithMessage("Query cannot exceed 500 characters.");

            RuleFor(x => x.Request.TechnicalQuery)
               .MaximumLength(500).WithMessage("TechnicalQuery cannot exceed 500 characters.");

            RuleFor(x => x.Request.MinPrice)
                .GreaterThanOrEqualTo(0).WithMessage("MinPrice must be greater than or equal to 0.")
                .When(x => x.Request.MinPrice.HasValue);

            RuleFor(x => x.Request.MaxPrice)
                .GreaterThanOrEqualTo(0).WithMessage("MaxPrice must be greater than or equal to 0.")
                .When(x => x.Request.MaxPrice.HasValue);

            RuleFor(x => x.Request)
                .Must(x => !x.MinPrice.HasValue || !x.MaxPrice.HasValue || x.MinPrice <= x.MaxPrice)
                .WithMessage("MinPrice cannot exceed MaxPrice.");
        }
    }
}
