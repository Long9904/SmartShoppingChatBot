using Microsoft.AspNetCore.Http;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.ProductCommon;

internal static class ProductActorResolver
{
    public static async Task<Result<UserEmbedded>> ResolveAsync(
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        Business business)
    {
        var authenticationType = httpContextAccessor.HttpContext?.User.Identity?.AuthenticationType;


        if ("ApiKey".Equals(authenticationType, StringComparison.OrdinalIgnoreCase))
        {
            return Result<UserEmbedded>.Success(new UserEmbedded
            {
                Name = "Business: " + business.BusinessName
            });
        }
        else
        {
            var user = await currentUserService.GetUser();
            if (!user.IsSuccess || user.Data == null)
            {
                return Result<UserEmbedded>.Failure(
                    user.StatusCode,
                    user.Message,
                    user.Errors,
                    user.MessageCode);
            }

            return Result<UserEmbedded>.Success(new UserEmbedded
            {
                Id = user.Data.Id,
                Name = user.Data.FullName
            });
        }
    }
}
