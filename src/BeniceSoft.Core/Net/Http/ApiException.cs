namespace BeniceSoft.Http.FluentClient;

/// <summary>
/// API business exception thrown when ResponseResult.Code != 200.
/// </summary>
public class ApiException : Exception
{
    public int Code { get; }

    public ApiException(int code, string message) : base(message)
    {
        Code = code;
    }
}
