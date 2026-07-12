using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using System.Text.Json.Serialization;

namespace SmartShoppingChatBot.Application.Features.SystemContentManagement.UpdateSystemContent;

public class UpdateSystemContentCommand : IRequest<Result<SystemContentResponse>>
{
    [JsonIgnore]
    public ObjectId SystemContentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
