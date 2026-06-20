using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;

namespace SmartShoppingChatBot.Application.Features.GetMyBusinessProfile;

public class GetMyBusinessProfileQueryHandler : IRequestHandler<GetMyBusinessProfileQuery, Result<BusinessResponse>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public GetMyBusinessProfileQueryHandler(ICurrentUserService currentUserService, IMapper mapper)
    {
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<BusinessResponse>> Handle(
        GetMyBusinessProfileQuery request,
        CancellationToken cancellationToken)
    {
        var currentBusinessResult = await _currentUserService.GetBusiness();
        if (!currentBusinessResult.IsSuccess)
        {
            return Result<BusinessResponse>.Failure(
                currentBusinessResult.StatusCode,
                currentBusinessResult.Message,
                currentBusinessResult.Errors);
        }

        var response = _mapper.Map<BusinessResponse>(currentBusinessResult.Data);
        return Result<BusinessResponse>.Success(response, 200, "Business profile retrieved successfully.");
    }
}
