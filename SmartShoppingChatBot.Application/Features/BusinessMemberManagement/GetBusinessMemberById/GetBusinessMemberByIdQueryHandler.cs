using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.BusinessMemberManagement.GetBusinessMemberById;

public class GetBusinessMemberByIdQueryHandler : IRequestHandler<GetBusinessMemberByIdQuery, Result<ProfileResponse>>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;

    public GetBusinessMemberByIdQueryHandler(
        IUserRepository userRepository,
        ICurrentUserService currentUserService,
        IMapper mapper)
    {
        _userRepository = userRepository;
        _currentUserService = currentUserService;
        _mapper = mapper;
    }

    public async Task<Result<ProfileResponse>> Handle(
        GetBusinessMemberByIdQuery request,
        CancellationToken cancellationToken)
    {
        var businessResult = await _currentUserService.GetBusiness();
        if (!businessResult.IsSuccess)
        {
            return Result<ProfileResponse>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors);
        }

        var businessId = businessResult.Data!.Id;
        var member = await _userRepository.FindAsync(user =>
            user.Id == request.MemberId &&
            user.Business.Id == businessId &&
            user.Business.Role == RoleEnums.CATALOG_TEAM &&
            user.UserStatus != UserStatus.DELETED);

        if (member == null)
        {
            return Result<ProfileResponse>.Failure(404, "Catalog team member not found.");
        }

        var response = _mapper.Map<ProfileResponse>(member);
        return Result<ProfileResponse>.Success(response, 200, "Catalog team member retrieved successfully.");
    }
}
