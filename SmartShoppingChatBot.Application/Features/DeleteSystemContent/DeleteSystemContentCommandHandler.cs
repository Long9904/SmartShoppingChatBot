using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.DeleteSystemContent;

public class DeleteSystemContentCommandHandler : IRequestHandler<DeleteSystemContentCommand, Result<SystemContentResponse>>
{
    private readonly ISystemContentRepository _systemContentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<DeleteSystemContentCommandHandler> _logger;
    private readonly TimeProvider _time;

    public DeleteSystemContentCommandHandler(
        ISystemContentRepository systemContentRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<DeleteSystemContentCommandHandler> logger,
        TimeProvider time)
    {
        _systemContentRepository = systemContentRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
        _time = time;
    }

    public async Task<Result<SystemContentResponse>> Handle(
        DeleteSystemContentCommand request,
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

        var currentUserResult = await _currentUserService.GetUser();
        if (!currentUserResult.IsSuccess)
        {
            return Result<SystemContentResponse>.Failure(
                currentUserResult.StatusCode,
                currentUserResult.Message,
                currentUserResult.Errors);
        }

        var now = _time.GetUtcNow();
        var currentUser = currentUserResult.Data!;
        systemContent.Status = SystemContentStatus.Deleted;
        systemContent.DeletedAt = now;
        systemContent.UpdatedAt = now;
        systemContent.UpdatedBy = new UserEmbedded
        {
            Id = currentUser.Id,
            Name = currentUser.FullName
        };

        try
        {
            await _systemContentRepository.UpdateAsync(systemContent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting system content {SystemContentId}.", systemContent.Id);
            return Result<SystemContentResponse>.Failure(500, "An error occurred while deleting the system content.");
        }

        var response = _mapper.Map<SystemContentResponse>(systemContent);
        return Result<SystemContentResponse>.Success(response, 200, "System content deleted successfully.");
    }
}
