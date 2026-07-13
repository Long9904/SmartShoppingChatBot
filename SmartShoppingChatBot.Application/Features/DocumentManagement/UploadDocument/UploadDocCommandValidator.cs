using FluentValidation;

namespace SmartShoppingChatBot.Application.Features.DocumentManagement.UploadDocument
{
    public class UploadDocCommandValidator : AbstractValidator<UploadDocCommand>
    {
        private readonly string[] _allowedExtensions = [".pdf", ".docx", ".txt"];
        public UploadDocCommandValidator()
        {
            RuleForEach(x => x.Files)
           .Must(file => file.Length > 0)
           .WithMessage("File is empty.")
           .Must(file => file.Length <= 10 * 1024 * 1024)
           .WithMessage("File size must be less than or equal to 10MB.")
           .Must(file =>
           {
               var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
               return _allowedExtensions.Contains(ext);
           })
           .WithMessage("Only PDF, DOCX, and TXT files are allowed.");
        }
    }
}
