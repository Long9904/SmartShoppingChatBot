using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.UpdateMyProfile
{
    public class UpdateMyProfileCommandValidation : FluentValidation.AbstractValidator<UpdateMyProfileCommand>
    {
        public UpdateMyProfileCommandValidation()
        {
            RuleFor(command => command.Hotline)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Hotline is required.")
                .Matches(@"^(03|05|07|08|09)\d{8}$")
                .WithMessage("Invalid hotline format. It should start with 03, 05, 07, 08, or 09 followed by 8 digits.");
            RuleFor(command => command.WebsiteUrl)
                        .Cascade(CascadeMode.Stop)
                        .NotEmpty().WithMessage("Website URL is required.")
                        .Must(BeAValidUrl).WithMessage("Invalid URL format.");
            RuleFor(command => command.AddressLine)
                .NotEmpty().WithMessage("Address line is required.");

            RuleFor(x => x.DateOfBirth)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithMessage("Date of birth is required.")
                .LessThan(DateTime.Now).WithMessage("Date of birth must be in the past.")
                .GreaterThan(DateTime.Now.AddYears(-120)).WithMessage("Date of birth must be within a reasonable range.");

            RuleFor(x => x.Gender)
                .Cascade(CascadeMode.Stop)
                .NotNull().WithMessage("Gender is required.")
                .InclusiveBetween(0, 2).WithMessage("Gender must be either 0 (Female), 1 (Male), or 2 (Other).");
        }
        private bool BeAValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
                   && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }
    }
}
