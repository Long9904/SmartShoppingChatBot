namespace SmartShoppingChatBot.Application.DTOs
{
    public class ApproveBusinessEmployeeEmailModel
    {
        public string? VerificationToken { get; set; }
        public string? BusinessName { get; set; }
        public string? EmployeeEmail { get; set; }
        public string? EmployeeName { get; set; }
    }
}
