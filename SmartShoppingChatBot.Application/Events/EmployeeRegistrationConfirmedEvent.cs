namespace SmartShoppingChatBot.Application.Events
{
    public class EmployeeRegistrationConfirmedEvent
    {
        public string? BusinessName { get; set; }
        public string? EmployeeName { get; set; }
        public string? EmployeeEmail { get; set; }
        public string? TokenVerification { get; set; }
    }
}
