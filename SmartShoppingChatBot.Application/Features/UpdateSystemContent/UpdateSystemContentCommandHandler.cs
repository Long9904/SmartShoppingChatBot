using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.UpdateSystemContent;

public class UpdateSystemContentCommandHandler : IRequestHandler<UpdateSystemContentCommand, Result<SystemContentResponse>>
{
    private readonly ISystemContentRepository _systemContentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<UpdateSystemContentCommandHandler> _logger;
    private readonly TimeProvider _time;

    public UpdateSystemContentCommandHandler(
        ISystemContentRepository systemContentRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<UpdateSystemContentCommandHandler> logger,
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
        UpdateSystemContentCommand request,
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

        var duplicateContent = await _systemContentRepository.FindAsync(content =>
            content.Id != request.SystemContentId &&
            content.Key == request.Key.Trim() &&
            content.Status != SystemContentStatus.Deleted &&
            content.DeletedAt == null);

        if (duplicateContent != null)
        {
            return Result<SystemContentResponse>.Failure(409, "A system content with this key already exists.");
        }

        var currentUserResult = await _currentUserService.GetUser();
        if (!currentUserResult.IsSuccess)
        {
            return Result<SystemContentResponse>.Failure(
                currentUserResult.StatusCode,
                currentUserResult.Message,
                currentUserResult.Errors);
        }

        var currentUser = currentUserResult.Data!;
        systemContent.Title = request.Title.Trim();
        systemContent.Key = request.Key.Trim();
        systemContent.Content = request.Content.Trim();
        systemContent.ContentType = Enum.Parse<ContentType>(request.ContentType, true);
        systemContent.Status = Enum.Parse<SystemContentStatus>(request.Status, true);
        systemContent.Version += 1;
        systemContent.UpdatedAt = _time.GetUtcNow();
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
            _logger.LogError(ex, "An error occurred while updating system content {SystemContentId}.", systemContent.Id);
            return Result<SystemContentResponse>.Failure(500, "An error occurred while updating the system content.");
        }

        var response = _mapper.Map<SystemContentResponse>(systemContent);
        return Result<SystemContentResponse>.Success(response, 200, "System content updated successfully.");
    }
}
