using FluentValidation;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductGetAll;

public class ProductGetAllQueryValidator : AbstractValidator<ProductGetAllQuery>
{
    private static readonly HashSet<string> AllowedOrderBy = new(StringComparer.OrdinalIgnoreCase)
    {
        "ExternalId", "ExternalId asc", "ExternalId desc",
        "Name", "Name asc", "Name desc",
        "Price", "Price asc", "Price desc",
        "Currency", "Currency asc", "Currency desc",
        "Brand", "Brand asc", "Brand desc",
        "StockQuantity", "StockQuantity asc", "StockQuantity desc",
        "Category", "Category asc", "Category desc",
        "Status", "Status asc", "Status desc",
        "CreatedAt", "CreatedAt asc", "CreatedAt desc",
        "UpdatedAt", "UpdatedAt asc", "UpdatedAt desc"
    };

    public ProductGetAllQueryValidator()
    {
        RuleFor(x => x.Filter.PageIndex)
            .GreaterThan(0).WithMessage("PageIndex must be greater than 0.");

        RuleFor(x => x.Filter.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");

        RuleFor(x => x.Filter.ExternalId)
            .MaximumLength(100).WithMessage("ExternalId cannot exceed 100 characters.");

        RuleFor(x => x.Filter.Name)
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters.");

        RuleFor(x => x.Filter.Category)
            .MaximumLength(100).WithMessage("Category cannot exceed 100 characters.");

        RuleFor(x => x.Filter.MinPrice)
            .GreaterThanOrEqualTo(0).When(x => x.Filter.MinPrice.HasValue)
            .WithMessage("MinPrice must be greater than or equal to 0.");

        RuleFor(x => x.Filter.MaxPrice)
            .GreaterThanOrEqualTo(0).When(x => x.Filter.MaxPrice.HasValue)
            .WithMessage("MaxPrice must be greater than or equal to 0.");

        RuleFor(x => x)
            .Must(x => !x.Filter.MinPrice.HasValue
                || !x.Filter.MaxPrice.HasValue
                || x.Filter.MaxPrice.Value >= x.Filter.MinPrice.Value)
            .WithMessage("MaxPrice must be greater than or equal to MinPrice.");

        RuleFor(x => x.Filter.MinStockQuantity)
            .GreaterThanOrEqualTo(0).When(x => x.Filter.MinStockQuantity.HasValue)
            .WithMessage("MinStockQuantity must be greater than or equal to 0.");

        RuleFor(x => x.Filter.MaxStockQuantity)
            .GreaterThanOrEqualTo(0).When(x => x.Filter.MaxStockQuantity.HasValue)
            .WithMessage("MaxStockQuantity must be greater than or equal to 0.");

        RuleFor(x => x)
            .Must(x => !x.Filter.MinStockQuantity.HasValue
                || !x.Filter.MaxStockQuantity.HasValue
                || x.Filter.MaxStockQuantity.Value >= x.Filter.MinStockQuantity.Value)
            .WithMessage("MaxStockQuantity must be greater than or equal to MinStockQuantity.");

        RuleFor(x => x.Filter.Status)
            .Must(status => !status.HasValue || Enum.IsDefined(typeof(ProductStatus), status.Value))
            .WithMessage("Invalid product status.");

        RuleFor(x => x.Filter.OrderBy)
            .NotEmpty().WithMessage("OrderBy is required.")
            .Must(orderBy => orderBy != null && AllowedOrderBy.Contains(orderBy.Trim()))
            .WithMessage("Invalid OrderBy value.");
    }
}
