namespace SmartShoppingChatBot.Application.Commons.Results;

public class Result<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public int StatusCode { get; set; }
    public string? Message { get; set; }

    public Dictionary<string, string>? Errors { get; set; }

    private Result(
        bool isSuccess,
        int statusCode,
        T? data,
        string? message = null,
        Dictionary<string, string>? errors = null)
    {
        IsSuccess = isSuccess;
        StatusCode = statusCode;
        Data = data;
        Message = message;
        Errors = errors;
    }

    public static Result<T> Success(T? data, int statusCode = 200, string? message = null)
        => new(true, statusCode, data, message);

    public static Result<T> Failure(
        int statusCode = 400,
        string? message = null,
        Dictionary<string, string>? errors = null)
        => new(false, statusCode, default, message)
        {
            Errors = errors
        };
}

