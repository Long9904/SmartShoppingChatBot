using AutoMapper;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Entities;

namespace SmartShoppingChatBot.Application.Commons.Mapper
{
    public class ProductProfile : Profile
    {
        public ProductProfile()
        {
            CreateMap<Product, ProductResponseV2>()
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.Id.ToString()))
                .ForMember(dest => dest.ExternalProductId, opt => opt.MapFrom(src => src.ExternalId))
                .ForMember(dest => dest.ExternalProductUrl, opt => opt.MapFrom(src => src.ExternalProductUrl))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
                .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Price + " " + src.Currency))
                .ForMember(dest => dest.Brand, opt => opt.MapFrom(src => src.Brand))
                .ForMember(dest => dest.StockQuantity, opt => opt.MapFrom(src => src.StockQuantity))
                .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.Category))
                .ForMember(dest => dest.Images, opt => opt.MapFrom(src => src.Images))
                .ForMember(dest => dest.Metadata, opt => opt.MapFrom(src => src.Metadata));

            CreateMap<Product, ProductResponseV3>()
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.Id.ToString()))
                .ForMember(dest => dest.ExternalProductId, opt => opt.MapFrom(src => src.ExternalId))
                .ForMember(dest => dest.ExternalProductUrl, opt => opt.MapFrom(src => src.ExternalProductUrl))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Price + " " + src.Currency))
                .ForMember(dest => dest.Brand, opt => opt.MapFrom(src => src.Brand))
                .ForMember(dest => dest.StockQuantity, opt => opt.MapFrom(src => src.StockQuantity))
                .ForMember(dest => dest.Category, opt => opt.MapFrom(src => src.Category))
                .ForMember(dest => dest.Images, opt => opt.MapFrom(src => src.Images))
                .ForMember(dest => dest.Score, opt => opt.Ignore());
        }

    }
}
