using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.BusinessQuotaManagement.GetBusinessQuotas;

public class GetBusinessQuotasQueryValidator : AbstractValidator<GetBusinessQuotasQuery>
{
    private static readonly HashSet<string> AllowedOrderBy = new(StringComparer.OrdinalIgnoreCase)
    {
        "InputTokens", "InputTokens asc", "InputTokens desc",
        "OutputTokens", "OutputTokens asc", "OutputTokens desc",
        "CreatedAt", "CreatedAt asc", "CreatedAt desc"
    };

    public GetBusinessQuotasQueryValidator()
    {
        RuleFor(x => x.Filter.PageIndex)
            .GreaterThan(0).WithMessage("PageIndex must be greater than 0.");

        RuleFor(x => x.Filter.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");

        RuleFor(x => x.Filter.SourceType)
            .IsInEnum().When(x => x.Filter.SourceType.HasValue)
            .WithMessage("Invalid source type.");

        RuleFor(x => x.Filter.OrderBy)
            .NotEmpty().WithMessage("OrderBy is required.")
            .Must(orderBy => AllowedOrderBy.Contains(orderBy.Trim()))
            .WithMessage("OrderBy must be InputTokens, OutputTokens, or CreatedAt, optionally followed by asc or desc.");
    }
}
