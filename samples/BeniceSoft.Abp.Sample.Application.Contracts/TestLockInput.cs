using System.Runtime.Serialization;

namespace BeniceSoft.Abp.Sample.Application.Contracts;

/// <summary>
/// 测试锁输入参数
/// 添加 [DataContract] 特性以支持 gRPC/protobuf 序列化
/// </summary>
[DataContract]
public class TestLockInput
{
    [DataMember(Order = 1)]
    public long Id { get; set; }

    /// <summary>
    /// 模拟业务执行时长（毫秒），默认 0 表示使用各测试接口内置时长
    /// </summary>
    [DataMember(Order = 2)]
    public int DelayMilliseconds { get; set; }
}
