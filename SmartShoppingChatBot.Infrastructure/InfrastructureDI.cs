using Microsoft.Extensions.DependencyInjection;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Infrastructure.Repositories;
using SmartShoppingChatBot.Infrastructure.Seeders;
using SmartShoppingChatBot.Infrastructure.Services;

namespace SmartShoppingChatBot.Infrastructure;

public static class InfrastructureDI
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

        // Services
        services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
        services.AddSingleton<IHashService, HashService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IGeminiService, GeminiService>();
        services.AddScoped<IQwenService, QwenService>();
        services.AddScoped<ICloudinaryService, CloudinaryService>();
        services.AddScoped<IQdrantService, QdrantService>();

        // Seeder
        services.AddScoped<UserSeeder>();
        services.AddScoped<QdrantCollectionInitializer>();

        // Repo
        services.AddScoped<IBusinessRepository, BusinessRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITokenRepository, TokenRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<ISystemContentRepository, SystemContentRepository>();
        services.AddScoped<IBusinessQuotaRepository, BusinessQuotaRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();

        services.AddScoped<IKnowledgeDocumentRepository, KnowledgeDocumentRepository>();

        return services;
    }
}
