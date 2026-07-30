using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.CustomerManagement.GetCustomers;

public sealed class GetCustomersQueryValidator : AbstractValidator<GetCustomersQuery>
{
    private static readonly HashSet<string> AllowedOrderBy = new(StringComparer.OrdinalIgnoreCase)
    {
        "CustomerExternalId", "CustomerExternalId asc", "CustomerExternalId desc",
        "Name", "Name asc", "Name desc",
        "Status", "Status asc", "Status desc",
        "CreatedAt", "CreatedAt asc", "CreatedAt desc",
        "UpdatedAt", "UpdatedAt asc", "UpdatedAt desc"
    };

    public GetCustomersQueryValidator()
    {
        RuleFor(query => query.Filter.PageIndex)
            .GreaterThan(0).WithMessage("PageIndex must be greater than 0.");

        RuleFor(query => query.Filter.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");

        RuleFor(query => query.Filter.CustomerExternalId)
            .MaximumLength(50).WithMessage("CustomerExternalId cannot exceed 50 characters.");

        RuleFor(query => query.Filter.Status)
            .Must(status => !status.HasValue || Enum.IsDefined(status.Value))
            .WithMessage("Invalid customer status.");

        RuleFor(query => query.Filter.OrderBy)
            .NotEmpty().WithMessage("OrderBy is required.")
            .Must(orderBy => AllowedOrderBy.Contains(orderBy.Trim()))
            .WithMessage("Invalid OrderBy value.");
    }
}
