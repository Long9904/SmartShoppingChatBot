using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.API.Middlewares
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly IAuthService _authService;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IAuthService authService)
            : base(options, logger, encoder)
        {
            _authService = authService;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("x-api-key", out var apiKey))
                return AuthenticateResult.NoResult();

            if (string.IsNullOrEmpty(apiKey)) return AuthenticateResult.NoResult();

            var business = await _authService.ValidateApiKeyAsync(apiKey);

            if (!business.IsSuccess)
                return AuthenticateResult.Fail("Invalid API Key");

            var claims = new List<Claim>
            {
                new Claim("business" , business.Data!.Id.ToString())
            };


            var identity = new ClaimsIdentity(claims, Scheme.Name);

            var principal = new ClaimsPrincipal(identity);

            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);


        }
    }
}
