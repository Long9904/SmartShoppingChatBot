using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SmartShoppingChatBot.Application.Commons.Behaviors;
using SmartShoppingChatBot.Application.Commons.Mapper;
namespace SmartShoppingChatBot.Application;

public static class ApplicationDI
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // FluentValidation registration
        services.AddValidatorsFromAssemblyContaining(typeof(ApplicationDI));

        // MediatR registration
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining(typeof(ApplicationDI)));

        // Use custom validation behavior
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // AutoMapper registration
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(AutoMapperDI).Assembly));

        return services;
    }
}

