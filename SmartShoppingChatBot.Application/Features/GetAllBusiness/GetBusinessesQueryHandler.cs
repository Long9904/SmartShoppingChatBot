using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.GetAllBusiness
{
    public class GetBusinessesQueryHandler :
        IRequestHandler<GetBusinessesQuery, Result<BasePaginatedList<BusinessResponse>>>
    {
        private readonly IBusinessRepository _businessRepository;
        private readonly IMapper _mapper;

        public GetBusinessesQueryHandler(IBusinessRepository businessRepository, IMapper mapper)
        {
            _businessRepository = businessRepository;
            _mapper = mapper;
        }


        public async Task<Result<BasePaginatedList<BusinessResponse>>> Handle(
            GetBusinessesQuery request,
            CancellationToken cancellationToken)
        {
            var query = _businessRepository.AsQueryable();

            if (!string.IsNullOrEmpty(request.Filter?.Search))
            {
                query = query.Where(b => b.BusinessName.Contains(request.Filter.Search));
            }

            if (request.Filter?.Status.HasValue == true)
            {
                query = query.Where(b => b.BusinessStatus == request.Filter.Status.Value);

            }
            if (request.Filter?.CreatedFrom.HasValue == true)
            {
                query = query.Where(b => b.CreatedAt >= request.Filter.CreatedFrom.Value);
            }


            var pagingList = await _businessRepository.PaginatedListAsync(
                query,
                request.Filter?.PageIndex ?? 1,
                request.Filter?.PageSize ?? 10);


            var responseItems = _mapper.Map<IReadOnlyCollection<BusinessResponse>>(pagingList.Items);

            return Result<BasePaginatedList<BusinessResponse>>.Success(new BasePaginatedList<BusinessResponse>
            {
                Items = responseItems,
                TotalItems = pagingList.TotalItems,
                TotalPages = pagingList.TotalPages,
                PageIndex = pagingList.PageIndex,
                PageSize = pagingList.PageSize
            });
        }
    }
}
