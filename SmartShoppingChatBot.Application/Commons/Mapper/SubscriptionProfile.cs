using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Commons.Mapper
{
    public class SubscriptionProfile : AutoMapper.Profile
    {
        public SubscriptionProfile() { 
            CreateMap<SubscriptionPlan, SubscriptionResponse>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()));
        }
    }
}
