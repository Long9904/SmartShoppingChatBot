using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.KernelSemanticSearch.ProductGetByExternalIds;

public sealed class ProductGetByExternalIdsQueryValidator
    : AbstractValidator<ProductGetByExternalIdsQuery>
{
    public ProductGetByExternalIdsQueryValidator()
    {
        RuleFor(query => query.Request).NotNull();
        When(query => query.Request is not null, () =>
        {
            RuleFor(query => query.Request.ExternalProductIds)
                .Cascade(CascadeMode.Stop)
                .NotNull()
                .NotEmpty()
                .Must(ids => ids.Count <= 100)
                .WithMessage("Chỉ được lấy tối đa 100 externalProductId mỗi lần.");
            RuleForEach(query => query.Request.ExternalProductIds)
                .NotEmpty()
                .MaximumLength(100);
        });
    }
}
