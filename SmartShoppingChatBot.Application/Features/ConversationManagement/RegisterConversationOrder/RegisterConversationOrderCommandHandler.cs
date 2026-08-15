using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Enums;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.RegisterConversationOrder;

public sealed class RegisterConversationOrderCommandHandler(
    ICurrentUserService currentUserService,
    IConversationRepository conversationRepository,
    IConversationOrderRepository orderRepository,
    IConversationOrderEventRepository orderEventRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<RegisterConversationOrderCommandHandler> logger)
    : IRequestHandler<RegisterConversationOrderCommand, Result<ConversationOrderResponse>>
{
    public async Task<Result<ConversationOrderResponse>> Handle(
        RegisterConversationOrderCommand request,
        CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(request.ConversationId, out var conversationId))
        {
            return Result<ConversationOrderResponse>.Failure(
                400,
                "Invalid conversation ID.",
                messageCode: ConversationMessageCode.InvalidId);
        }

        var businessResult = await currentUserService.GetBusiness();
        if (!businessResult.IsSuccess || businessResult.Data is null)
        {
            return Result<ConversationOrderResponse>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors,
                businessResult.MessageCode);
        }

        var businessId = businessResult.Data.Id;
        var conversation = await conversationRepository.FindAsync(item =>
            item.Id == conversationId && item.BusinessId == businessId);
        if (conversation is null)
        {
            return Result<ConversationOrderResponse>.Failure(
                404,
                "Conversation not found.",
                messageCode: ConversationMessageCode.NotFound);
        }

        var externalOrderId = request.Order.ExternalOrderId.Trim();
        var existingOrder = await orderRepository.FindAsync(item =>
            item.BusinessId == businessId && item.ExternalOrderId == externalOrderId);
        if (existingOrder is not null)
        {
            return Result<ConversationOrderResponse>.Failure(
                409,
                "External order ID is already registered.",
                messageCode: ConversationOrderMessageCode.AlreadyExists);
        }

        var now = timeProvider.GetUtcNow();
        var products = request.Order.Products.Select(ToSnapshot).ToList();
        var order = new ConversationOrder
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = businessId,
            ConversationId = conversationId,
            ExternalOrderId = externalOrderId,
            Status = ConversationOrderEventStatus.OrderCreated,
            Amount = request.Order.Amount,
            CreatedAt = now,
            UpdatedAt = now,
            ProductOrderSnapshotItems = products
        };
        var orderEvent = new ConversationOrderEvent
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = businessId,
            ConversationId = conversationId,
            ExternalOrderId = externalOrderId,
            Status = ConversationOrderEventStatus.OrderCreated,
            Amount = order.Amount,
            CreatedAt = now,
            ProductOrderSnapshotItems = products.Select(CopySnapshot).ToList()
        };

        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            await orderRepository.AddAsync(order);
            await orderEventRepository.AddAsync(orderEvent);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            await unitOfWork.RollBackAsync(cancellationToken);
            logger.LogError(
                exception,
                "Could not register external order {ExternalOrderId} for conversation {ConversationId}",
                externalOrderId,
                request.ConversationId);

            return Result<ConversationOrderResponse>.Failure(
                500,
                "Could not register conversation order.",
                messageCode: ConversationOrderMessageCode.ServerError);
        }

        return Result<ConversationOrderResponse>.Success(
            ConversationOrderResponse.FromEntity(order),
            201,
            "Conversation order registered.",
            ConversationOrderMessageCode.Success);
    }

    private static ProductOrderSnapshot ToSnapshot(ProductOrderSnapshotRequest product) => new()
    {
        ExternalProductId = product.ExternalProductId.Trim(),
        ProductName = product.ProductName?.Trim(),
        Price = product.Price,
        Quantity = product.Quantity
    };

    private static ProductOrderSnapshot CopySnapshot(ProductOrderSnapshot product) => new()
    {
        ExternalProductId = product.ExternalProductId,
        ProductName = product.ProductName,
        Price = product.Price,
        Quantity = product.Quantity
    };
}
