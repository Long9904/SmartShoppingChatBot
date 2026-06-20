using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.UpdateBusiness;

public class UpdateBusinessCommandValidator : AbstractValidator<UpdateBusinessCommand>
{
    public UpdateBusinessCommandValidator()
    {
        RuleFor(command => command.BusinessName)
            .NotEmpty().WithMessage("Business name is required.");

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
    }

    private bool BeAValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}
