using AutoMapper;
using MediatR;
using OpenAI.Responses;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartShoppingChatBot.Application.Features.ActivityLogManagement.GetActivityLog
{
    public class GetActivityLogQueryHandler : IRequestHandler<GetActivityLogQuery, Result<BasePaginatedList<ActivityLogResponse>>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IActivityLogRepository _activityLogRepository;
        private readonly IMapper _mapper;
        private readonly ICurrentUserService _currentUserService;

        public GetActivityLogQueryHandler(IUnitOfWork unitOfWork, IActivityLogRepository activityLogRepository, IMapper mapper, ICurrentUserService currentUserService)
        {
            _unitOfWork = unitOfWork;
            _activityLogRepository = activityLogRepository;
            _mapper = mapper;
            _currentUserService = currentUserService;
        }


        public async Task<Result<BasePaginatedList<ActivityLogResponse>>> Handle(GetActivityLogQuery request, CancellationToken cancellationToken)
        {
            var user = await _currentUserService.GetUser();
            if (user == null || user.Data == null)
            {
                return Result<BasePaginatedList<ActivityLogResponse>>.Failure(400, "User not found");
            }
            var query = _activityLogRepository.AsQueryable();
            if (user.Data.Business.Role == Domain.Enums.RoleEnums.ADMIN)
            {
                if (!string.IsNullOrWhiteSpace(request.Filter?.BusinessId))
                {
                    query = query.Where(x => x.BusinessId == request.Filter.BusinessId);
                }

            }
            else if (user.Data.Business.Role == Domain.Enums.RoleEnums.BUSINESS_OWNER || user.Data.Business.Role == Domain.Enums.RoleEnums.CATALOG_TEAM)
            {
                var bussiness = await _currentUserService.GetBusiness();
                if (bussiness == null || bussiness.Data == null)
                {
                    return Result<BasePaginatedList<ActivityLogResponse>>.Failure(400, "Business not found");
                }

                query = query.Where(x => x.BusinessId == bussiness.Data.Id.ToString());

            }
            else
            {
                return Result<BasePaginatedList<ActivityLogResponse>>.Failure(403, "Forbidden");
            }
            if(request.Filter != null)
            {
                if (!string.IsNullOrWhiteSpace(request.Filter.Keyword))
                {
                    var keyword = request.Filter.Keyword.Trim();
                    query = query.Where(x =>
                        (x.ActorEmail != null && x.ActorEmail.Contains(keyword)) ||
                        (x.Description != null && x.Description.Contains(keyword)) ||
                        (x.TargetType != null && x.TargetType.Contains(keyword)) ||
                        (x.TargetId != null && x.TargetId.Contains(keyword)) ||
                        (x.MetadataJson != null && x.MetadataJson.Contains(keyword)));
                }
                if (request.Filter.Action != null)
                {
                    query = query.Where(x => x.Action == request.Filter.Action);
                }
                if (request.Filter.Status != null)
                {
                    query = query.Where(x => x.Status == request.Filter.Status);
                }
                if (request.Filter.Severity != null)
                {
                    query = query.Where(x => x.Severity == request.Filter.Severity);
                }
                if(!string.IsNullOrWhiteSpace(request.Filter.ActorId))
                {
                    query = query.Where(x => x.ActorId == request.Filter.ActorId);
                }
                if(!string.IsNullOrWhiteSpace(request.Filter.TargetType))
                {
                    query = query.Where(x => x.TargetType == request.Filter.TargetType);
                }
                if(!string.IsNullOrWhiteSpace(request.Filter.TargetId))
                {
                    query = query.Where(x => x.TargetId == request.Filter.TargetId);
                }
                if(request.Filter.ToDate != null || request.Filter.FromDate != null)
                {
                    if(request.Filter.FromDate != null)
                    {
                        query = query.Where(x => x.CreatedAt >= request.Filter.FromDate.Value);
                    }
                    if(request.Filter.ToDate != null)
                    {
                        var toDate = request.Filter.ToDate.Value;
                        var toDateExclusive = new DateTimeOffset(
                            toDate.Year,
                            toDate.Month,
                            toDate.Day,
                            0,
                            0,
                            0,
                            toDate.Offset).AddDays(1);

                        query = query.Where(x => x.CreatedAt < toDateExclusive);
                    }
                }

            }
            query = query.OrderByDescending(x => x.CreatedAt);
            var paginatedList = await _activityLogRepository.PaginatedListAsync(query, request.Filter?.PageIndex ?? 1, request.Filter?.PageSize ?? 10);
            var response = _mapper.Map<IReadOnlyCollection<ActivityLogResponse>>(paginatedList.Items);
            return Result<BasePaginatedList<ActivityLogResponse>>.Success(new BasePaginatedList<ActivityLogResponse>
            {
                Items = response,
                TotalItems = paginatedList.TotalItems,
                TotalPages = paginatedList.TotalPages,
                PageIndex = paginatedList.PageIndex,
                PageSize = paginatedList.PageSize
            }, 200, "Get activity logs successfully");
        }
    }
}
