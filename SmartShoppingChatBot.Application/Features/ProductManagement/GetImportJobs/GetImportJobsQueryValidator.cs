using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.GetImportJobs;

public class GetImportJobsQueryValidator : AbstractValidator<GetImportJobsQuery>
{
    public GetImportJobsQueryValidator()
    {
        RuleFor(x => x.Filter.PageIndex)
            .GreaterThan(0).WithMessage("PageIndex must be greater than 0.");

        RuleFor(x => x.Filter.PageSize)
            .InclusiveBetween(1, 100).WithMessage("PageSize must be between 1 and 100.");

        RuleFor(x => x.Filter.FileName)
            .MaximumLength(255).WithMessage("FileName cannot exceed 255 characters.");

        RuleFor(x => x.Filter.Status)
            .IsInEnum().When(x => x.Filter.Status.HasValue)
            .WithMessage("Invalid import job status.");
    }
}
