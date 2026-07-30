using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using SmartShoppingChatBot.Domain.Commons;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.GetImportJobs;

public class GetImportJobsQuery : IRequest<Result<BasePaginatedList<ImportJobResponse>>>
{
    public GetImportJobsFilter Filter { get; set; } = new();
}
