using AutoMapper;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Commons.Mapper
{
    public class ImportJobMapper : Profile
    {
        public ImportJobMapper()
        {
            CreateMap<ImportJob, ImportJobResponse>().ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()));
        }
    }
}
