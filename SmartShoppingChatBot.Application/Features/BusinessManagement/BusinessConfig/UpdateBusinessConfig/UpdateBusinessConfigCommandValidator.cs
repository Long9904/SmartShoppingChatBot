using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.BusinessManagement.BusinessConfig.UpdateBusinessConfig;

public class UpdateBusinessConfigCommandValidator : AbstractValidator<UpdateBusinessConfigCommand>
{
    public UpdateBusinessConfigCommandValidator()
    {
        RuleFor(command => command.ModelTemperature)
            .NotNull().WithMessage("Model temperature is required.")
            .InclusiveBetween(0.0, 1.0).WithMessage("Model temperature must be between 0.0 and 1.0.");

        RuleFor(command => command.TopKDocument)
            .NotNull().WithMessage("Top K document is required.")
            .InclusiveBetween(1, 5).WithMessage("Top K document must be between 1 and 5.");

        RuleFor(command => command.RerankingScore)
            .NotNull().WithMessage("Reranking score is required.")
            .InclusiveBetween(0.4, 0.95).WithMessage("Reranking score must be between 0.4 and 0.95.");

        RuleFor(command => command.SystemPrompt)
            .MaximumLength(100).WithMessage("System prompt must not exceed 100 characters.");

        RuleFor(command => command.FallBackMessage)
            .MaximumLength(100).WithMessage("Fallback message must not exceed 100 characters.");

        RuleFor(command => command.MaxOutPutToken)
            .NotNull().WithMessage("Max output token is required.")
            .InclusiveBetween(1500, 4000).WithMessage("Max output token must be between 1500 and 4000.");
    }
}
