using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.KernelSemanticSearch.CategorySemanticSearch;

public sealed class CategorySemanticSearchQueryValidator
    : AbstractValidator<CategorySemanticSearchQuery>
{
    public CategorySemanticSearchQueryValidator()
    {
        RuleFor(query => query.Request).NotNull();
        When(query => query.Request is not null, () =>
        {
            RuleFor(query => query.Request.Category)
                .NotEmpty().When(query => string.IsNullOrWhiteSpace(query.Request.Bm25Query))
                .MaximumLength(200);
            RuleFor(query => query.Request.Bm25Query)
                .MaximumLength(120)
                .When(query => query.Request.Bm25Query is not null);
            RuleFor(query => query.Request.Attributes)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .Must(attributes => attributes.Count <= 12)
                .WithMessage("Chỉ được truyền tối đa 12 thuộc tính.");
            RuleForEach(query => query.Request.Attributes).NotNull().ChildRules(attribute =>
            {
                attribute.RuleFor(value => value.Name).NotEmpty().MaximumLength(100);
                attribute.RuleFor(value => value.Value).NotEmpty().MaximumLength(200);
            });
            RuleFor(query => query.Request.PriceBand)
                .IsInEnum();
            RuleFor(query => query.Request.MinPrice)
                .GreaterThanOrEqualTo(0)
                .When(query => query.Request.MinPrice.HasValue);
            RuleFor(query => query.Request.MaxPrice)
                .GreaterThanOrEqualTo(0)
                .When(query => query.Request.MaxPrice.HasValue);
            RuleFor(query => query.Request)
                .Must(request => !request.MinPrice.HasValue
                    || !request.MaxPrice.HasValue
                    || request.MinPrice <= request.MaxPrice)
                .WithMessage("Giá tối thiểu không được lớn hơn giá tối đa.");
            RuleFor(query => query.Request.ExcludeProductIds)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .Must(ids => ids.Count <= 100)
                .WithMessage("Chỉ được loại trừ tối đa 100 sản phẩm.");
        });
    }
}
