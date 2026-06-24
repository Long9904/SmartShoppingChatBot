using FluentValidation;
using MongoDB.Bson;

namespace SmartShoppingChatBot.Application.Features.ApiKeyManagement.RevokeApiKey
{
    public class RevokeApiKeyCommandValidator : AbstractValidator<RevokeApiKeyCommand>
    {
        public RevokeApiKeyCommandValidator()
        {
            RuleFor(x => x.Id)
                .NotEmpty().WithMessage("API key ID is required.")
                .Must(x => ObjectId.TryParse(x, out _)).WithMessage("Invalid ID.");
        }
    }
}
