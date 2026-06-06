using SmartShoppingChatBot.Application.DTOs;

namespace SmartShoppingChatBot.Application.Commons.Results
{
    public class ApiResponse<T>
    {
        public bool IsSuccess { get; init; }
        public string? Message { get; init; }
        public T? Data { get; init; }
        public Dictionary<string, string>? Errors { get; init; }

        private ApiResponse(
            bool isSuccess,
            string? message,
            T? data,
            Dictionary<string, string>? errors)
        {
            IsSuccess = isSuccess;
            Message = message;
            Data = data;
            Errors = errors;
        }

        public static ApiResponse<T> Ok(
            T data,
            string? message = null)
            => new(true, message, data, null);

        public static ApiResponse<T> Fail(
            string message,
            Dictionary<string, string>? errors = null)
            => new(false, message, default, errors);

    }
}
