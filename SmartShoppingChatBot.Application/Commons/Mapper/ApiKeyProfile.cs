using AutoMapper;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Commons.Mapper
{
    public class ApiKeyProfile : Profile
    {
        public ApiKeyProfile()
        {
            CreateMap<ApiKey, ApiKeyResponse>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()));
        }
    }
}
