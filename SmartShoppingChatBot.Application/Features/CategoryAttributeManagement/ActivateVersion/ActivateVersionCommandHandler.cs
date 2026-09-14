using MediatR;
using Microsoft.Extensions.Logging;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Commons;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.ActivateVersion;

public sealed class ActivateVersionCommandHandler : IRequestHandler<ActivateVersionCommand, Result<bool>>
{
    private readonly ICategoryAttributeSchemaRepository _repository;
    private readonly ICategoryAttributeSchemaService _schemaService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ActivateVersionCommandHandler> _logger;

    public ActivateVersionCommandHandler(
        ICategoryAttributeSchemaRepository repository,
        ICategoryAttributeSchemaService schemaService,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<ActivateVersionCommandHandler> logger)
    {
        _repository = repository;
        _schemaService = schemaService;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        ActivateVersionCommand request,
        CancellationToken cancellationToken)
    {
        var currentUserResult = await _currentUserService.GetUser();
        if (!currentUserResult.IsSuccess)
        {
            return Result<bool>.Failure(
                currentUserResult.StatusCode,
                currentUserResult.Message,
                currentUserResult.Errors);
        }

        var category = request.Category.Trim();

        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            var schemas = await _repository.GetByCategoryAsync(category);
            var targetSchema = schemas.FirstOrDefault(schema => schema.Version == request.Version);
            if (targetSchema is null)
            {
                await _unitOfWork.RollBackAsync(CancellationToken.None);
                return Result<bool>.Failure(404, "Category attribute schema version not found.");
            }

            if (targetSchema.IsActive)
            {
                await _unitOfWork.RollBackAsync(CancellationToken.None);
                return Result<bool>.Success(true, 200, "Category attribute schema version is already active.");
            }

            var now = _timeProvider.GetUtcNow();
            var activeSchemas = schemas.Where(schema => schema.IsActive).ToList();
            foreach (var activeSchema in activeSchemas)
            {
                activeSchema.IsActive = false;
                activeSchema.UpdatedAt = now;
            }

            targetSchema.IsActive = true;
            targetSchema.UpdatedAt = now;
            targetSchema.ApprovedAt = now;
            targetSchema.ApprovedBy = new UserEmbedded
            {
                Id = currentUserResult.Data!.Id,
                Name = currentUserResult.Data.FullName
            };

            await _repository.UpdateRangeAsync(activeSchemas);
            await _repository.UpdateAsync(targetSchema);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            await _unitOfWork.RollBackAsync(CancellationToken.None);
            _logger.LogError(
                exception,
                "Could not activate category attribute schema {Category} version {Version}.",
                category,
                request.Version);
            return Result<bool>.Failure(500, "An error occurred while activating the category attribute schema version.");
        }

        await _schemaService.InvalidateAsync(category);
        return Result<bool>.Success(true, 200, "Category attribute schema version activated successfully.");
    }
}
