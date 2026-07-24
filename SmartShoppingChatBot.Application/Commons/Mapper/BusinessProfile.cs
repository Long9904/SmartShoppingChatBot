using AutoMapper;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Commons.Mapper;

public class BusinessProfile : Profile
{
    public BusinessProfile()
    {
        CreateMap<Business, BusinessRegistrationResponse>();
        CreateMap<Business, BusinessResponse>();
        CreateMap<Business, MyBusinessProfileResponse>();
        CreateMap<Business, BusinessResponseV1>()
            .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()))
            .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.BusinessName));
    }
}
