using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SmartShoppingChatBot.Application.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class ExtractFileService : IExtractFileService
    {
        public Task<string> ExtractMarkdownAsync(Stream stream, string fileType)
        {
            var normalizedType = fileType.Trim().TrimStart('.').ToLowerInvariant();

            return normalizedType switch
            {
                "docx" => ExtractDocxAsync(stream),
                "txt" => ExtractTxtAsync(stream),
                "pdf" => ExtractPdfAsync(stream),
                _ => throw new NotSupportedException($"Unsupported document type: {fileType}")
            };
        }

        public async Task<string> ExtractDocxAsync(Stream stream)
        {
            if (stream.CanSeek)
                stream.Position = 0;

            using var document = WordprocessingDocument.Open(stream, false);

            var body = document.MainDocumentPart?.Document?.Body;
            if (body == null) return string.Empty;

            var sb = new StringBuilder();

            foreach (var element in body.Elements())
            {
                if (element is Paragraph paragraph)
                {
                    var text = paragraph.InnerText.Trim();
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var styleId = paragraph.ParagraphProperties?
                        .ParagraphStyleId?
                        .Val?
                        .Value;

                    if (IsHeading(styleId, out var level))
                    {
                        sb.AppendLine($"{new string('#', level)} {text}");
                        sb.AppendLine();
                    }
                    else
                    {
                        sb.AppendLine(text);
                        sb.AppendLine();
                    }
                }
            }
            return sb.ToString().Trim();
        }

        private bool IsHeading(string? styleId, out int level)
        {
            level = 1;

            if (string.IsNullOrWhiteSpace(styleId))
                return false;

            if (!styleId.StartsWith("Heading", StringComparison.OrdinalIgnoreCase))
                return false;

            var numberPart = styleId.Replace("Heading", "");

            if (int.TryParse(numberPart, out var parsedLevel))
                level = Math.Clamp(parsedLevel, 1, 6);

            return true;
        }

        public Task<string> ExtractPdfAsync(Stream stream)
        {
            throw new NotSupportedException("PDF extraction is not implemented yet.");
        }

        public Task<string> ExtractTxtAsync(Stream stream)
        {
            if (stream.CanSeek)
                stream.Position = 0;

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return reader.ReadToEndAsync();
        }
    }
}
