using AutoMapper;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Commons.Mapper
{
    public class AutoMapperDI : Profile
    {
        public AutoMapperDI()
        {
            // Generic mapping for BasePaginatedList<T>
            CreateMap(typeof(BasePaginatedList<>), typeof(BasePaginatedList<>))
                .ConvertUsing(typeof(BasePaginatedListConverter<,>));
        }
    }

    // Generic converter for BasePaginatedList
    public class BasePaginatedListConverter<TSource, TDestination> : ITypeConverter<BasePaginatedList<TSource>, BasePaginatedList<TDestination>>
        where TSource : class
        where TDestination : class
    {
        public BasePaginatedList<TDestination> Convert(BasePaginatedList<TSource> source, BasePaginatedList<TDestination> destination, ResolutionContext context)
        {
            var mappedItems = context.Mapper.Map<List<TDestination>>(source.Items.ToList());
            return new BasePaginatedList<TDestination>(mappedItems, source.TotalItems, source.PageIndex, source.PageSize);
        }
    }
}
