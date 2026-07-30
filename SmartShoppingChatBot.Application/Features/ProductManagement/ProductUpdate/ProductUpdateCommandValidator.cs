using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductUpdate;

public class ProductUpdateCommandValidator : AbstractValidator<ProductUpdateCommand>
{
    public ProductUpdateCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.ProductId.HasValue ^ !string.IsNullOrWhiteSpace(x.LookupExternalId))
            .WithMessage("Exactly one product identifier is required.");

        RuleFor(x => x.ExternalId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("ExternalId is required.")
            .MaximumLength(100).WithMessage("ExternalId cannot exceed 100 characters.");

        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");

        RuleFor(x => x.Description)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(200).WithMessage("Description cannot exceed 200 characters.");

        RuleFor(x => x.ExternalProductUrl)
            .NotEmpty().WithMessage("External product URL is required.")
            .MaximumLength(500).WithMessage("External product URL cannot exceed 500 characters.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price must be greater than or equal to 0.");

        RuleFor(x => x.Currency)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Currency is required.")
            .MaximumLength(10).WithMessage("Currency cannot exceed 10 characters.");

        RuleFor(x => x.Brand)
            .MaximumLength(100).WithMessage("Brand cannot exceed 100 characters.");

        RuleFor(x => x.StockQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("StockQuantity must be greater than or equal to 0.");

        RuleFor(x => x.Category)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(100).WithMessage("Category cannot exceed 100 characters.");

        RuleFor(x => x.Images)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Images list cannot be null.")
            .Must(images => images.Count <= 10).WithMessage("Images list cannot contain more than 10 items.");

        RuleForEach(x => x.Images)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Image URL cannot be empty.")
            .MaximumLength(500).WithMessage("Image URL cannot exceed 500 characters.");

        RuleFor(x => x.Metadata)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Metadata cannot be null.")
            .Must(metadata => metadata.Count <= 20).WithMessage("Metadata cannot contain more than 20 items.");

        RuleForEach(x => x.Metadata)
            .Cascade(CascadeMode.Stop)
            .Must(item => !string.IsNullOrWhiteSpace(item.Key)).WithMessage("Metadata key cannot be empty.")
            .Must(item => item.Key.Length <= 100).WithMessage("Metadata key cannot exceed 100 characters.")
            .Must(item => item.Value != null).WithMessage("Metadata value cannot be null.");
    }
}
