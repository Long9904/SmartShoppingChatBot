using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.SystemContentManagement.GetSystemContentByKey;

public class GetSystemContentByKeyQueryHandler : IRequestHandler<GetSystemContentByKeyQuery, Result<SystemContentResponse>>
{
    private readonly ISystemContentRepository _systemContentRepository;
    private readonly IMapper _mapper;

    public GetSystemContentByKeyQueryHandler(
        ISystemContentRepository systemContentRepository,
        IMapper mapper)
    {
        _systemContentRepository = systemContentRepository;
        _mapper = mapper;
    }

    public async Task<Result<SystemContentResponse>> Handle(
        GetSystemContentByKeyQuery request,
        CancellationToken cancellationToken)
    {
        var systemContent = await _systemContentRepository.FindAsync(content =>
            content.Key.Contains(request.Key.Trim()) &&
            content.Status == SystemContentStatus.Published &&
            content.DeletedAt == null);

        if (systemContent == null)
        {
            return Result<SystemContentResponse>.Failure(404, "Published system content not found.");
        }

        var response = _mapper.Map<SystemContentResponse>(systemContent);
        return Result<SystemContentResponse>.Success(response, 200, "System content retrieved successfully.");
    }
}
