using AutoMapper;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Commons.Mapper;

public class BusinessProfile : Profile
{
    public BusinessProfile()
    {
        CreateMap<Business, BusinessRegistrationResponse>();
    }
}
