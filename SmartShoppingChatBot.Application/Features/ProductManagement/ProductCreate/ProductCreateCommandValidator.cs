using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductCreate
{
    public class ProductCreateCommandValidator : AbstractValidator<ProductCreateCommand>
    {
        public ProductCreateCommandValidator()
        {

            RuleFor(x => x.ExternalId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("ExternalId is required.")
                .MaximumLength(100).WithMessage("ExternalId cannot exceed 100 characters.");

            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");

            RuleFor(x => x.Description)
                .NotEmpty().WithMessage("Description is required")
                .MaximumLength(200).WithMessage("Description cannot exxcedd 200 characrer.");

            RuleFor(x => x.ExternalProductUrl)
                .NotEmpty().WithMessage("External product url cannot null");

            RuleFor(x => x.Price)
                .GreaterThanOrEqualTo(0).WithMessage("Price must be greater than or equal to 0.");

            RuleFor(x => x.Currency)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Currency is required.")
                .MaximumLength(10).WithMessage("Currency cannot exceed 10 characters.");

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
                .NotNull().WithMessage("Image URL cannot be null.")
                .NotEmpty().WithMessage("Image URL cannot be empty.")
                .MaximumLength(500).WithMessage("Image URL cannot exceed 500 characters.");

            RuleFor(x => x.Metadata)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithMessage("Metadata cannot be null.")
                .Must(metadata => metadata.Count <= 20).WithMessage("Metadata cannot contain more than 20 items.");

            RuleForEach(x => x.Metadata)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithMessage("Metadata cannot be null.")
                .Must(kv => !string.IsNullOrWhiteSpace(kv.Key)).WithMessage("Metadata key cannot be empty.")
                .Must(kv => kv.Value != null).WithMessage("Metadata value cannot be null.")
                .Must(kv => kv.Value.Length <= 100).WithMessage("Metadata value cannot exceed 100 characters.")
                .Must(kv => kv.Key.Length <= 20).WithMessage("Metadata key cannot exceed 20 characters.");

        }
    }
}
