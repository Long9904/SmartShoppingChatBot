using MediatR;
using MongoDB.Bson;
using SmartShoppingChatBot.Application.Commons.Results;
using SmartShoppingChatBot.Application.DTOs;
using System.Text.Json.Serialization;

namespace SmartShoppingChatBot.Application.Features.UserManagement.UpdateUser
{
    public class UpdateUserCommand : IRequest<Result<ProfileResponse>>
    {
        [JsonIgnore]
        public ObjectId UserId { get; set; }
        public string FullName { get; init; } = string.Empty;
        public string? PhoneNumber { get; init; }
        public DateTime? DateOfBirth { get; init; }
        public int? Gender { get; init; }
    }
}
