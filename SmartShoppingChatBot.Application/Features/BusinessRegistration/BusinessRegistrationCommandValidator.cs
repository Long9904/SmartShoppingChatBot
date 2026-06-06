using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.BusinessRegistration;

public class BusinessRegistrationCommandValidator : AbstractValidator<BusinessRegistrationCommand>
{
    public BusinessRegistrationCommandValidator()
    {
        RuleFor(command => command.BusinessName)
            .NotEmpty().WithMessage("Business name is required.");

        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.");

        RuleFor(command => command.HotLine)
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

        RuleFor(command => command.City)
            .NotEmpty().WithMessage("City is required.");

        RuleFor(command => command.BrandAssetsUrl)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Brand assets URL is required.")
            .Must(BeAValidUrl).WithMessage("Invalid URL format.");
    }

    private bool BeAValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}
