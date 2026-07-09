namespace SmartShoppingChatBot.Application.Commons.Results
{
    public class ApiResponse<T>
    {
        public bool IsSuccess { get; init; }
        public string? Message { get; init; }

        public string? MessageCode { get; init; }
        public T? Data { get; init; }
        public Dictionary<string, string>? Errors { get; init; }

        private ApiResponse(
            bool isSuccess,
            string? message,
            string? messageCode,
            T? data,
            Dictionary<string, string>? errors)
        {
            IsSuccess = isSuccess;
            Message = message;
            MessageCode = messageCode;
            Data = data;
            Errors = errors;
        }

        public static ApiResponse<T> Ok(
            T data,
            string? message = null,
            string? messageCode = null)
            => new(true, message, messageCode, data, null);

        public static ApiResponse<T> Fail(
            string message,
            Dictionary<string, string>? errors = null,
            string? messageCode = null)
            => new(false, message, messageCode, default, errors);

    }
}
