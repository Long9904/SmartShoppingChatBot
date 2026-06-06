using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.BusinessRegistration
{
    public record BusinessRegistrationCommand : IRequest<Result<BusinessRegistrationResponse>>
    {
        public string BusinessName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string HotLine { get; init; } = string.Empty;
        public string WebsiteUrl { get; init; } = string.Empty;
        public string AddressLine { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
        public string BrandAssetsUrl { get; init; } = string.Empty;
    }
}
