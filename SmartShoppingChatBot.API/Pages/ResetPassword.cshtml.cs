using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmartShoppingChatBot.Application.Features.ProfileManagement.ResetPassword;

namespace SmartShoppingChatBot.API.Pages
{
    public class ResetPasswordModel : PageModel
    {
        private readonly IMediator _mediator;

        [BindProperty]
        public ResetPasswordCommand Command { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string Email { get; set; } = string.Empty;

        public string Message { get; private set; } = string.Empty;
        public bool HasSubmitted { get; private set; }
        public bool IsSuccess { get; private set; }
        public Dictionary<string, string> Errors { get; private set; } = new();

        public ResetPasswordModel(IMediator mediator)
        {
            _mediator = mediator;
        }

        public void OnGet(string? token, string? email)
        {
            Command = new ResetPasswordCommand
            {
                Token = token ?? string.Empty
            };
            Email = email ?? string.Empty;

            if (string.IsNullOrWhiteSpace(Command.Token))
            {
                Message = "Reset password token is missing.";
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            HasSubmitted = true;

            if (!ModelState.IsValid)
            {
                CollectModelStateErrors();
                Message = "Please check the highlighted fields.";
                return Page();
            }

            var result = await _mediator.Send(Command);
            IsSuccess = result.IsSuccess;
            Errors = result.Errors ?? new Dictionary<string, string>();
            Message = result.Message ?? (result.IsSuccess
                ? "Password reset successfully."
                : "Password reset failed. Please try again.");

            return Page();
        }

        private void CollectModelStateErrors()
        {
            Errors = ModelState
                .Where(item => item.Value?.Errors.Count > 0)
                .ToDictionary(
                    item => ToCamelCase(item.Key.Replace($"{nameof(Command)}.", string.Empty)),
                    item => string.Join(" ", item.Value!.Errors.Select(error =>
                        string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "Invalid value."
                            : error.ErrorMessage)));
        }

        private static string ToCamelCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return char.ToLowerInvariant(value[0]) + value[1..];
        }
    }
}
