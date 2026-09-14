using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.GetHistory;

public sealed class GetHistoryQueryValidator : AbstractValidator<GetHistoryQuery>
{
    public GetHistoryQueryValidator()
    {
        RuleFor(query => query.Category)
            .NotEmpty().WithMessage("Category is required.")
            .MaximumLength(250).WithMessage("Category must not exceed 250 characters.");
    }
}
