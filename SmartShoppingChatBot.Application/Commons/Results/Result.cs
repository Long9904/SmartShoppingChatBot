namespace SmartShoppingChatBot.Application.Commons.Results;

public class Result<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }

    public int? StatusCode { get; set; }

    public string? Message { get; set; }

    public static Result<T> Success(T data, int? statusCode = null, string? message = null)
    {
        return new Result<T>
        {
            IsSuccess = true,
            Data = data,
            StatusCode = statusCode,
            Message = message
        };
    }

    public static Result<T> Failure(string message, int? statusCode = null)
    {
        return new Result<T>
        {
            IsSuccess = false,
            Data = default,
            StatusCode = statusCode,
            Message = message
        };
    }
}



public class Result
{
    public bool IsSuccess { get; set; }
    public int? StatusCode { get; set; }
    public string? Message { get; set; }
    public static Result Success(int? statusCode = null, string? message = null)
    {
        return new Result
        {
            IsSuccess = true,
            StatusCode = statusCode,
            Message = message
        };
    }
    public static Result Failure(string message, int? statusCode = null)
    {
        return new Result
        {
            IsSuccess = false,
            StatusCode = statusCode,
            Message = message
        };
    }
}

