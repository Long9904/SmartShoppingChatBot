using AutoMapper;
using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.GetSystemContentById;

public class GetSystemContentByIdQueryHandler : IRequestHandler<GetSystemContentByIdQuery, Result<SystemContentResponse>>
{
    private readonly ISystemContentRepository _systemContentRepository;
    private readonly IMapper _mapper;

    public GetSystemContentByIdQueryHandler(
        ISystemContentRepository systemContentRepository,
        IMapper mapper)
    {
        _systemContentRepository = systemContentRepository;
        _mapper = mapper;
    }

    public async Task<Result<SystemContentResponse>> Handle(
        GetSystemContentByIdQuery request,
        CancellationToken cancellationToken)
    {
        var systemContent = await _systemContentRepository.FindAsync(content =>
            content.Id == request.SystemContentId &&
            content.Status != SystemContentStatus.Deleted &&
            content.DeletedAt == null);

        if (systemContent == null)
        {
            return Result<SystemContentResponse>.Failure(404, "System content not found.");
        }

        var response = _mapper.Map<SystemContentResponse>(systemContent);
        return Result<SystemContentResponse>.Success(response, 200, "System content retrieved successfully.");
    }
}
