using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.UpdateBusinessMember;

public class UpdateBusinessMemberCommandValidator : AbstractValidator<UpdateBusinessMemberCommand>
{
    public UpdateBusinessMemberCommandValidator()
    {
        RuleFor(command => command.FullName)
            .NotEmpty().WithMessage("Full name is required.");
    }
}
