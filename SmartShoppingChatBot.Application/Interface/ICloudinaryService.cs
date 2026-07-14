using Microsoft.AspNetCore.Http;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface ICloudinaryService
    {
        Task<Result<UploadedFileResponse>> UploadFileAsync(IFormFile file, string businessId, string folderName);
        Task<Result<Stream>> DownloadFileAsync(string urlFile);
    }
}
