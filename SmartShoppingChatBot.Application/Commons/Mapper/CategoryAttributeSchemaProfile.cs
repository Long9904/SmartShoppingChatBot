using AutoMapper;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Commons.Mapper;

public sealed class CategoryAttributeSchemaProfile : Profile
{
    public CategoryAttributeSchemaProfile()
    {
        CreateMap<UserEmbedded, CategoryAttributeSchemaUserResponse>()
            .ForMember(destination => destination.Id, options =>
                options.MapFrom(source => source.Id.ToString()));

        CreateMap<CategoryAttributeSchema, CategoryAttributeSchemaResponse>()
            .ForMember(destination => destination.Id, options =>
                options.MapFrom(source => source.Id.ToString()));
    }
}
