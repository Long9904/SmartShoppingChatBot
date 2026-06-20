
using AutoMapper;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;


namespace SmartShoppingChatBot.Application.Commons.Mapper;

public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, ProfileResponse>()
           .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
           .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
           .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
           .ForMember(dest => dest.IsEmailVerified, opt => opt.MapFrom(src => src.IsEmailVerified))
           .ForMember(dest => dest.IsProfileCompleted, opt => opt.MapFrom(src => src.IsProfileCompleted))
           .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PhoneNumber))
           .ForMember(dest => dest.DateOfBirth, opt => opt.MapFrom(src => src.DateOfBirth))
           .ForMember(dest => dest.BusinessName, opt => opt.MapFrom(src => src.Business.BusinessName))
           .ForMember(dest => dest.UserStatus, opt => opt.MapFrom(src => src.UserStatus))
           .ForMember(dest => dest.Role, opt => opt.MapFrom(src => src.Business.Role))
           .ForMember(dest => dest.JoinedAt, opt => opt.MapFrom(src => src.Business.JoinedAt));
    }
}
