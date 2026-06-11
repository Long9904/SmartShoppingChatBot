
using AutoMapper;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Commons.Mapper;

public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<BusinessEmbedded, BusinessLoginResponse>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()))
            .ForMember(dest => dest.BusinessName, opt => opt.MapFrom(src => src.BusinessName))
            .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Role));
    }
}
