using System.Net;
using System.Text.Json.Serialization;

namespace BeniceSoft.Core;

/// <summary>
/// 返回结果包装类
/// </summary>
public class ResponseResult
{
    /// <summary>
    /// 状态码
    /// </summary>
    public int Code { get; set; } = 200;

    /// <summary>
    /// 信息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 链路追踪标识
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    [JsonIgnore]
    public bool IsSuccess => Code == 200;

    public ResponseResult()
    {
    }

    public ResponseResult(int code, string message)
    {
        Code = code;
        Message = message;
    }

    public ResponseResult(HttpStatusCode code, string message) : this((int)code, message)
    {
    }
}

/// <summary>
/// 返回结果包装类
/// </summary>
/// <typeparam name="T"></typeparam>
public class ResponseResult<T> : ResponseResult
{
    public ResponseResult()
    {
        Data = default!;
    }

    public ResponseResult(T td) => Data = td;

    public ResponseResult(int code, string message)
    {
        Code = code;
        Message = message;
        Data = default!;
    }

    public T Data { get; set; }
}
