using Microsoft.SemanticKernel;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Application.Plugins;
using SmartShoppingChatBot.Infrastructure.Services;

namespace SmartShoppingChatBot.API.Extensions;

public static class LegacyKernelRegistrationV3
{
    public static IServiceCollection PreserveLegacyKernelV3(this IServiceCollection services, IConfiguration configuration)
    {
        // Old endpoints still need their original tools and collector. The default kernel uses V3.
        services.AddScoped<IKernelChatService>(provider =>
        {
            var builder = Kernel.CreateBuilder();
            builder.AddOpenAIChatCompletion(configuration["OpenAI:ModelId"]!, configuration["OpenAI:ApiKey"]!);
            builder.Plugins.AddFromObject(ActivatorUtilities.CreateInstance<ProductPlugin>(provider), "Product");
            builder.Plugins.AddFromObject(provider.GetRequiredService<DocumentPlugin>(), "Document");
            return new KernelChatService(builder.Build(), provider.GetRequiredService<ILogger<KernelChatService>>(),
                provider.GetRequiredService<IRedisBusinessConfig>());
        });
        return services;
    }
}
