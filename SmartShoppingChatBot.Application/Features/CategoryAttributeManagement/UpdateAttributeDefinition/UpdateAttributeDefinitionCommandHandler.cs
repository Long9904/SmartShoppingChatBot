using AutoMapper;
using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.UpdateAttributeDefinition;

public sealed class UpdateAttributeDefinitionCommandHandler
    : IRequestHandler<UpdateAttributeDefinitionCommand, Result<CategoryAttributeSchemaResponse>>
{
    private readonly ICategoryAttributeSchemaRepository _repository;
    private readonly ICategoryAttributeSchemaService _schemaService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UpdateAttributeDefinitionCommandHandler> _logger;

    public UpdateAttributeDefinitionCommandHandler(
        ICategoryAttributeSchemaRepository repository,
        ICategoryAttributeSchemaService schemaService,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        TimeProvider timeProvider,
        ILogger<UpdateAttributeDefinitionCommandHandler> logger)
    {
        _repository = repository;
        _schemaService = schemaService;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<CategoryAttributeSchemaResponse>> Handle(
        UpdateAttributeDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        var category = request.Category.Trim();
        var schemas = await _repository.GetByCategoryAsync(category);
        var latestSchema = schemas.FirstOrDefault();
        if (latestSchema is null)
        {
            return Result<CategoryAttributeSchemaResponse>.Failure(404, "Category attribute schema not found.");
        }

        var attribute = latestSchema.Attributes.FirstOrDefault(item =>
            string.Equals(item.Key, request.AttributeKey, StringComparison.OrdinalIgnoreCase));
        if (attribute is null)
        {
            return Result<CategoryAttributeSchemaResponse>.Failure(404, "Attribute definition not found.");
        }

        var hasAllowedValues = request.AllowedValues is { Count: > 0 };
        if (attribute.DataType != AttributeDataType.Keyword && hasAllowedValues)
        {
            return Result<CategoryAttributeSchemaResponse>.Failure(
                400,
                "Only Keyword attributes can define allowedValues.");
        }

        if (attribute.DataType == AttributeDataType.Keyword && request.IsStrict && !hasAllowedValues)
        {
            return Result<CategoryAttributeSchemaResponse>.Failure(
                400,
                "Strict Keyword attributes require allowedValues.");
        }

        attribute.DisplayName = request.DisplayName.Trim();
        attribute.IsFilterable = request.IsFilterable;
        attribute.IsStrict = request.IsStrict;
        attribute.AllowedValues = request.AllowedValues?
            .Select(value => value.Trim())
            .ToList();
        latestSchema.UpdatedAt = _timeProvider.GetUtcNow();

        try
        {
            await _repository.UpdateAsync(latestSchema);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Could not update attribute {AttributeKey} of category {Category}.",
                request.AttributeKey,
                category);
            return Result<CategoryAttributeSchemaResponse>.Failure(
                500,
                "An error occurred while updating the attribute definition.");
        }

        if (latestSchema.IsActive)
        {
            await _schemaService.InvalidateAsync(category);
        }

        var response = _mapper.Map<CategoryAttributeSchemaResponse>(latestSchema);
        return Result<CategoryAttributeSchemaResponse>.Success(
            response,
            200,
            "Attribute definition updated successfully.");
    }
}
