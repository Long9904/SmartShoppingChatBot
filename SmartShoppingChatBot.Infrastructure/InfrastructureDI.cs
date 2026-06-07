using Microsoft.Extensions.DependencyInjection;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Infrastructure.Repositories;
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
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ITokenService, TokenService>();

        // Repo
        services.AddScoped<IBusinessRepository, BusinessRepository>();  
        services.AddScoped<IUserRepository, UserRepository>();


        return services;
    }
}
