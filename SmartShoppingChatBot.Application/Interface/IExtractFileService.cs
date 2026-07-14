namespace SmartShoppingChatBot.Application.Interface
{
    public interface IExtractFileService
    {
        Task<string> ExtractMarkdownAsync(Stream stream, string fileType);
        Task<string> ExtractDocxAsync(Stream stream);
        Task<string> ExtractPdfAsync(Stream stream);
        Task<string> ExtractTxtAsync(Stream stream);
    }
}
