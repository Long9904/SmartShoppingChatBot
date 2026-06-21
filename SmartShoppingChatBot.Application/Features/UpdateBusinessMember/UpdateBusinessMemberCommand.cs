using System.Text.Json.Serialization;
using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Features.UpdateBusinessMember;

public class UpdateBusinessMemberCommand : IRequest<Result<ProfileResponse>>
{
    [JsonIgnore]
    public ObjectId MemberId { get; set; }
    public string FullName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public int? Gender { get; init; }
}
