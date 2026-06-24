using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class ProfileResponse
    {
        public string Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public required string Email { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsProfileCompleted { get; set; }

        public string? PhoneNumber { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public int? Gender { get; set; }

        public UserStatus UserStatus { get; set; }

        public RoleEnums Role { get; set; }
        public string? BusinessName { get; set; } = null;
        public DateTimeOffset JoinedAt { get; set; }
    }
}
