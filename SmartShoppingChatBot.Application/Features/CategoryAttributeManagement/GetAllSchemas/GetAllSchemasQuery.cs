using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.GetAllSchemas;

public sealed record GetAllSchemasQuery : IRequest<Result<List<CategoryAttributeSchemaResponse>>>;
