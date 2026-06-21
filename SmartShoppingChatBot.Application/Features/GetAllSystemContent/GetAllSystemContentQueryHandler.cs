using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.GetAllSystemContent
{
    public class GetAllSystemContentQueryHandler : IRequestHandler<GetAllSystemContentQuery, Result<BasePaginatedList<object>>>
    {
        private readonly ISystemContentRepository _systemContentRepository;
        private readonly IMapper _mapper;

        public GetAllSystemContentQueryHandler(
            ISystemContentRepository systemContentRepository, 
            IMapper mapper)
        {
            _systemContentRepository = systemContentRepository;
            _mapper = mapper;
        }

        public async Task<Result<BasePaginatedList<object>>> Handle(GetAllSystemContentQuery request, CancellationToken cancellationToken)
        {
            var filter = request.Filter ?? new();

            var query = _systemContentRepository.AsQueryable()
                .Where(sc => 
                sc.Status != SystemContentStatus.Deleted 
                && sc.DeletedAt == null);

            if (!string.IsNullOrEmpty(filter.Title))
            {
                query = query.Where(sc => sc.Title.Contains(filter.Title));
            }

            if (filter.Status.HasValue)
            {
                query = query.Where(sc => sc.Status == filter.Status.Value);
            }

            if (filter.ContentType.HasValue)
            {
                query = query.Where(sc => sc.ContentType == filter.ContentType.Value);
            }

            if (!string.IsNullOrEmpty(filter.Key))
            {
                query = query.Where(sc => sc.Key.Contains(filter.Key));
            }

            var orderBy = filter.OrderBy ?? "CreatedAt desc";


            var mapperConfig = _mapper.ConfigurationProvider;

            var paging = await _systemContentRepository.GetAllWithPaggingSortSelectionFieldAsync<SystemContent, SystemContentResponse>(
                query, mapperConfig, orderBy, null, filter.PageIndex, filter.PageSize);

            return Result<BasePaginatedList<object>>.Success(paging, 200, "Get all system content successfully");

        }
    }
}
