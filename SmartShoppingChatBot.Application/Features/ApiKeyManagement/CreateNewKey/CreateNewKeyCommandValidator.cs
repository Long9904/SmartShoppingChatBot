using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.ApiKeyManagement.CreateNewKey
{
    public class CreateNewKeyCommandValidator : AbstractValidator<CreateNewKeyCommand>
    {
        public CreateNewKeyCommandValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");
        }
    }
}
