using SmartShoppingChatBot.Application.Commons.Queries;
using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.Features.ProductManagement.GetImportJobs;

public class GetImportJobsFilter : QueryBase
{
    public string? FileName { get; set; }

    public ImportJobStatus? Status { get; set; }
}
