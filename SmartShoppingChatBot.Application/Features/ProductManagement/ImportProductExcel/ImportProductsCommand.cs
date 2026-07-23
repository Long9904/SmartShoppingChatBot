using MediatR;
using Microsoft.AspNetCore.Http;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ImportProductExcel
{
    public sealed record ImportProductsCommand(
        IFormFile File
    ) : IRequest<Result<ImportJobResponse>>;
}
