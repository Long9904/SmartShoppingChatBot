using MediatR;
using SmartShoppingChatBot.Application.Commons.Results;

namespace SmartShoppingChatBot.Application.Features.CategoryAttributeManagement.ActivateVersion;

public sealed class ActivateVersionCommand : IRequest<Result<bool>>
{
    public string Category { get; set; } = string.Empty;
    public int Version { get; set; }
}
