using SmartShoppingChatBot.Domain.Enums;

namespace SmartShoppingChatBot.Application.DTOs
{
    public class ActivityLogRequest
    {
        public required ActionLogEnums Action { get; init; }
        public string? TargetType { get; init; }
        public string? TargetId { get; init; }
        public StatusLogEnums Status { get; init; } = StatusLogEnums.Success;
        public SeverityLogEnums Severity { get; init; } = SeverityLogEnums.Info;
        public string? Description { get; init; }
        public string? IpAddress { get; init; }
        public Dictionary<string, object?>? Metadata { get; init; }
    }
}
