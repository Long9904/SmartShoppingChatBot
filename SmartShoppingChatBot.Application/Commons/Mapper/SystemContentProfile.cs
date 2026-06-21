using AutoMapper;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Commons.Mapper;

public class SystemContentProfile : Profile
{
    public SystemContentProfile()
    {
        CreateMap<SystemContent, SystemContentResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()))
            .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy == null ? null : src.CreatedBy.Name))
            .ForMember(dest => dest.UpdatedBy, opt => opt.MapFrom(src => src.UpdatedBy == null ? null : src.UpdatedBy.Name));
    }
}
