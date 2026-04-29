namespace AutoTest.Application.Common.Models;

// Localized response wrapper. ErrorCode == 0 => success. HttpStatusCode + ErrorMessage
// are populated by ResponseService from the localization catalog based on X-Api-Lang.
public class BaseResponse
{
    public int ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public short HttpStatusCode { get; set; }

    public BaseResponse Error(int errorCode, string? errorMessage = null, short httpStatusCode = 0)
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        HttpStatusCode = httpStatusCode;
        return this;
    }

    public BaseResponse Success()
    {
        ErrorCode = 0;
        ErrorMessage = null;
        HttpStatusCode = 200;
        return this;
    }

    public static implicit operator bool(BaseResponse r) => r.ErrorCode == 0;
}

public class BaseResponse<T> : BaseResponse
{
    public T? Result { get; set; }

    public new BaseResponse<T> Error(int errorCode, string? errorMessage = null, short httpStatusCode = 0)
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        HttpStatusCode = httpStatusCode;
        return this;
    }

    public BaseResponse<T> Success(T result)
    {
        ErrorCode = 0;
        ErrorMessage = null;
        HttpStatusCode = 200;
        Result = result;
        return this;
    }
}
