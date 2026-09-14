using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.CreateDraft;

public sealed class CreateDraftCommandHandler : IRequestHandler<CreateDraftCommand, Result<CategoryAttributeSchemaResponse>>
{
    private readonly ICategoryAttributeSchemaRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreateDraftCommandHandler> _logger;

    public CreateDraftCommandHandler(
        ICategoryAttributeSchemaRepository repository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        TimeProvider timeProvider,
        ILogger<CreateDraftCommandHandler> logger)
    {
        _repository = repository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<CategoryAttributeSchemaResponse>> Handle(
        CreateDraftCommand request,
        CancellationToken cancellationToken)
    {
        var currentUserResult = await _currentUserService.GetUser();
        if (!currentUserResult.IsSuccess)
        {
            return Result<CategoryAttributeSchemaResponse>.Failure(
                currentUserResult.StatusCode,
                currentUserResult.Message,
                currentUserResult.Errors);
        }

        var category = request.Category.Trim();
        var history = await _repository.GetByCategoryAsync(category);
        var version = history.Count == 0 ? 1 : history.Max(schema => schema.Version) + 1;
        var now = _timeProvider.GetUtcNow();
        var currentUser = currentUserResult.Data!;

        var schema = new CategoryAttributeSchema
        {
            Id = ObjectId.GenerateNewId(),
            Category = category,
            Version = version,
            IsActive = false,
            Attributes = request.Attributes,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = new UserEmbedded
            {
                Id = currentUser.Id,
                Name = currentUser.FullName
            }
        };

        try
        {
            await _repository.AddAsync(schema);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Could not create category attribute draft for {Category}.", category);
            return Result<CategoryAttributeSchemaResponse>.Failure(500, "An error occurred while creating the category attribute draft.");
        }

        var response = _mapper.Map<CategoryAttributeSchemaResponse>(schema);
        return Result<CategoryAttributeSchemaResponse>.Success(response, 201, "Category attribute draft created successfully.");
    }
}
