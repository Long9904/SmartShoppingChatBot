using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Commons.Mapper
{
    public class PaymentProfile : BusinessProfile
    {
        public PaymentProfile()
        {
            CreateMap<Payment, PaymentResponse>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()))
                .ForMember(dest => dest.Bussiness, opt => opt.Ignore())
                .ForMember(dest => dest.SubscriptionPlan, opt => opt.Ignore());

        }
    }
}
