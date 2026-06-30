using Microsoft.AspNetCore.Http;
using System;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Interface
{
    public interface ICloudinaryService
    {
        Task<Result<UploadedFileResponse>> UploadFileAsync(IFormFile file, string businessId, string folderName);
    }
}
