using FluentValidation;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ImportProductExcel
{
    public class ImportProductRequestValidator : AbstractValidator<ImportProductRequest>
    {
        private const long MaxFileSize = 10 * 1024 * 1024;

        public ImportProductRequestValidator()
        {
            RuleFor(x => x.File)
                .NotNull()
                .WithMessage("File can not null");

            When(x => x.File is not null, () =>
            {
                RuleFor(x => x.File.Length)
                    .GreaterThan(0)
                    .WithMessage("File can not null")
                    .LessThanOrEqualTo(MaxFileSize)
                    .WithMessage("File data npt exceed 10 MB");

                RuleFor(x => x.File.FileName)
                    .Must(HaveValidExtension)
                    .WithMessage("Only support .xlsx.");
            });
        }

        private static bool HaveValidExtension(string fileName)
        {
            return string.Equals(
                Path.GetExtension(fileName),
                ".xlsx",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
