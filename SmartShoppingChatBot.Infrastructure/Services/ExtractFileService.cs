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
        public async Task<string> ExtractMarkdownAsync(Stream stream, string fileType)
        {
            //categorize file type
            var normalizedType = fileType.Trim().TrimStart('.').ToLowerInvariant();

            return normalizedType switch
            {
                "docx" => await ExtractDocxAsync(stream),
                "txt" => await ExtractTxtAsync(stream),
                "pdf" => await ExtractPdfAsync(stream), //recently not implemented
                _ => throw new NotSupportedException($"Unsupported document type: {fileType}")
            };
        }

        public Task<string> ExtractDocxAsync(Stream stream)
        {
            //make position to 0
            if (stream.CanSeek)
                stream.Position = 0;
            //open the docx file(edit: false)
            using var document = WordprocessingDocument.Open(stream, false);
            //get the content BODY of the document
            var body = document.MainDocumentPart?.Document?.Body;
            if (body == null) return Task.FromResult(string.Empty);

            var sb = new StringBuilder();
            //scan each element in the body(heading,paragraph,...)
            foreach (var element in body.Elements())
            {
                //first only check for paragraph
                if (element is Paragraph paragraph)
                {
                    //get only text
                    var text = paragraph.InnerText.Trim();
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    //get style of paragraph(heading 1,2,normal)
                    var styleId = paragraph.ParagraphProperties?
                        .ParagraphStyleId?
                        .Val?
                        .Value;

                    //check heading and return num
                    if (IsHeading(styleId, out var level))
                    {
                        //#heanding 1, ##heading 2, ###heading 3, ####heading 4, #####heading 5, ######heading 6
                        sb.AppendLine($"{new string('#', level)} {text}");
                        sb.AppendLine();
                    }
                    else
                    {
                        //just text
                        sb.AppendLine(text);
                        sb.AppendLine();
                    }
                }
            }
            return Task.FromResult(sb.ToString().Trim());
        }
        //check if the style is heading and return the number of heading
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
        // Note: PDF extraction is not implemented yet
        public Task<string> ExtractPdfAsync(Stream stream)
        {
            throw new NotSupportedException("PDF extraction is not implemented yet.");
        }
        // Note: TXT extraction is straightforward, just read the stream as text
        public Task<string> ExtractTxtAsync(Stream stream)
        {
            if (stream.CanSeek)
                stream.Position = 0;

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
            return reader.ReadToEndAsync();
        }
    }
}
