using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.GetPendingApproval;

public sealed record GetPendingApprovalQuery : IRequest<Result<List<CategoryAttributeSchemaResponse>>>;
