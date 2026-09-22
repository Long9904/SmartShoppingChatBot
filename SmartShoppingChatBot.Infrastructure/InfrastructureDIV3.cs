using Microsoft.Extensions.DependencyInjection;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Infrastructure.Services;

namespace SmartShoppingChatBot.Infrastructure;

public static class InfrastructureDIV3
{
    public static IServiceCollection AddInfrastructureServicesV3(this IServiceCollection services)
    {
        services.AddScoped<IProductSemanticSearchByAI, ProductSemanticSearchByAI>();
        services.AddScoped<IProductReferenceCollectorV3, ProductReferenceCollectorV3>();
        services.AddScoped<IProductReferenceResolverV3, ProductReferenceResolverV3>();
        services.AddScoped<IConversationContextServiceV3, ConversationContextServiceV3>();
        services.AddScoped<IKernelChatServiceV3, KernelChatServiceV3>();
        return services;
    }
}
