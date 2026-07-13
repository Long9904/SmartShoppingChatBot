namespace SmartShoppingChatBot.Application.DTOs
{
    public class UploadedFileResponse
    {
        public string FileUrl { get; set; } = null!;
        public string PublicId { get; set; } = null!;
        public string FileName { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public long SizeInBytes { get; set; }
    }
}
