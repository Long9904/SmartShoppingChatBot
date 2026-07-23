using Microsoft.AspNetCore.Http;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class ImportProductRequest
    {
        public required IFormFile File { get; set; }
    }
}
