using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Infrastructure.Services
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly ICloudinary _cloudinary;

        public CloudinaryService(ICloudinary cloudinary)
        {
            _cloudinary = cloudinary;
        }

        public async Task<Result<Stream>> DownloadFileAsync(string urlFile)
        {
            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(urlFile);
            if (!response.IsSuccessStatusCode)
            {
                return Result<Stream>.Failure((int)response.StatusCode, "Cant download file");
            }
            var stream = await response.Content.ReadAsStreamAsync();
            return Result<Stream>.Success(stream);
        }

        public async Task<Result<UploadedFileResponse>> UploadFileAsync(IFormFile file, string businessId, string folderName)
        {
            if (file == null || file.Length == 0)
            {
                return Result<UploadedFileResponse>.Failure(400, "File is empty or null.");
            }
            var extenstion = Path.GetExtension(file.FileName); //.jpg, .png, etc.
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(file.FileName);// Get the file name without extension
            var folder = $"{businessId}/{folderName}";
            var fileName = $"{fileNameWithoutExtension}_{Guid.NewGuid()}";

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            ms.Position = 0; // Reset the stream position to the beginning

            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(file.FileName, ms),
                PublicId = fileName,
                Folder = folder,
                Overwrite = false,
            };

            var result = await _cloudinary.UploadAsync(uploadParams);
            if (result.Error != null)
            {
                return Result<UploadedFileResponse>.Failure(400, $"Error uploading file: {result.Error.Message}");
            }
            return Result<UploadedFileResponse>.Success(new UploadedFileResponse
            {
                FileUrl = result.SecureUrl.ToString(),
                PublicId = result.PublicId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                SizeInBytes = file.Length
            });
        }
    }
}
