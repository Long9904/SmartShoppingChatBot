using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.CreateSystemContent;

public class CreateSystemContentCommandHandler : IRequestHandler<CreateSystemContentCommand, Result<SystemContentResponse>>
{
    private readonly ISystemContentRepository _systemContentRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<CreateSystemContentCommandHandler> _logger;
    private readonly TimeProvider _time;

    public CreateSystemContentCommandHandler(
        ISystemContentRepository systemContentRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<CreateSystemContentCommandHandler> logger,
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
        CreateSystemContentCommand request,
        CancellationToken cancellationToken)
    {
        var duplicateContent = await _systemContentRepository.FindAsync(content =>
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

        var now = _time.GetUtcNow();
        var currentUser = currentUserResult.Data!;

        var systemContent = new SystemContent
        {
            Id = ObjectId.GenerateNewId(),
            Title = request.Title.Trim(),
            Key = request.Key.Trim(),
            Content = request.Content.Trim(),
            ContentType = Enum.Parse<ContentType>(request.ContentType, true),
            Version = 1,
            Status = Enum.Parse<SystemContentStatus>(request.Status, true),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = new UserEmbedded
            {
                Id = currentUser.Id,
                Name = currentUser.FullName
            },
            UpdatedBy = new UserEmbedded
            {
                Id = currentUser.Id,
                Name = currentUser.FullName
            }
        };

        try
        {
            await _systemContentRepository.AddAsync(systemContent);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while creating system content with key {SystemContentKey}.", request.Key);
            return Result<SystemContentResponse>.Failure(500, "An error occurred while creating the system content.");
        }

        var response = _mapper.Map<SystemContentResponse>(systemContent);
        return Result<SystemContentResponse>.Success(response, 201, "System content created successfully.");
    }
}
