using Microsoft.Extensions.DependencyInjection;
using SmartShoppingChatBot.Domain.Interface;
using SmartShoppingChatBot.Infrastructure.Repositories;

namespace SmartShoppingChatBot.Infrastructure;

public static class InfrastructureDI
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        return services;
    }
}
