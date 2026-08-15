using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.MessageCodeMapper;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Application.Interface;
using SmartShoppingChatBot.Domain.Entities;
using SmartShoppingChatBot.Domain.Interface;

namespace SmartShoppingChatBot.Application.Features.ConversationManagement.UpdateConversationOrderStatus;

public sealed class UpdateConversationOrderStatusCommandHandler(
    ICurrentUserService currentUserService,
    IConversationOrderRepository orderRepository,
    IConversationOrderEventRepository orderEventRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<UpdateConversationOrderStatusCommandHandler> logger)
    : IRequestHandler<UpdateConversationOrderStatusCommand, Result<ConversationOrderResponse>>
{
    public async Task<Result<ConversationOrderResponse>> Handle(
        UpdateConversationOrderStatusCommand request,
        CancellationToken cancellationToken)
    {
        var businessResult = await currentUserService.GetBusiness();
        if (!businessResult.IsSuccess || businessResult.Data is null)
        {
            return Result<ConversationOrderResponse>.Failure(
                businessResult.StatusCode,
                businessResult.Message,
                businessResult.Errors,
                businessResult.MessageCode);
        }

        var externalOrderId = request.ExternalOrderId.Trim();
        var order = await orderRepository.FindAsync(item =>
            item.BusinessId == businessResult.Data.Id
            && item.ExternalOrderId == externalOrderId);
        if (order is null)
        {
            return Result<ConversationOrderResponse>.Failure(
                404,
                "Conversation order not found.",
                messageCode: ConversationOrderMessageCode.NotFound);
        }

        // Payment providers retry webhooks. Returning the current state keeps this API idempotent.
        if (order.Status == request.Status)
        {
            return Result<ConversationOrderResponse>.Success(
                ConversationOrderResponse.FromEntity(order),
                200,
                "Conversation order already has this status.",
                ConversationOrderMessageCode.Success);
        }

        var now = timeProvider.GetUtcNow();
        order.Status = request.Status;
        order.UpdatedAt = now;

        var orderEvent = new ConversationOrderEvent
        {
            Id = ObjectId.GenerateNewId(),
            BusinessId = order.BusinessId,
            ConversationId = order.ConversationId,
            ExternalOrderId = order.ExternalOrderId,
            Status = order.Status,
            Amount = order.Amount,
            CreatedAt = now,
            ProductOrderSnapshotItems = order.ProductOrderSnapshotItems.Select(CopySnapshot).ToList()
        };

        try
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            await orderRepository.UpdateAsync(order);
            await orderEventRepository.AddAsync(orderEvent);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            await unitOfWork.RollBackAsync(cancellationToken);
            logger.LogError(
                exception,
                "Could not update status for external order {ExternalOrderId}",
                externalOrderId);

            return Result<ConversationOrderResponse>.Failure(
                500,
                "Could not update conversation order status.",
                messageCode: ConversationOrderMessageCode.ServerError);
        }

        return Result<ConversationOrderResponse>.Success(
            ConversationOrderResponse.FromEntity(order),
            200,
            "Conversation order status updated.",
            ConversationOrderMessageCode.Success);
    }

    private static ProductOrderSnapshot CopySnapshot(ProductOrderSnapshot product) => new()
    {
        ExternalProductId = product.ExternalProductId,
        ProductName = product.ProductName,
        Price = product.Price,
        Quantity = product.Quantity
    };
}
