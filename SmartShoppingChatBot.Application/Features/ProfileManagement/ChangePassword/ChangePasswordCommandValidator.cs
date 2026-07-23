using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.ProfileManagement.ChangePassword
{
    public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
    {
        public ChangePasswordCommandValidator()
        {

            RuleFor(x => x.currentPassword)
                .NotEmpty().WithMessage("Current password is required.")
                .MinimumLength(6).WithMessage("Current password must be at least 6 characters long.");
            RuleFor(x => x.newPassword)
                .NotEmpty().WithMessage("New password is required.")
                .MinimumLength(6).WithMessage("New password must be at least 6 characters long.");
            RuleFor(x => x.confirmPassword)
                .NotEmpty().WithMessage("Confirm password is required.")
                .Equal(x => x.newPassword).WithMessage("Confirm password must match the new password.");
        }
    }
}
