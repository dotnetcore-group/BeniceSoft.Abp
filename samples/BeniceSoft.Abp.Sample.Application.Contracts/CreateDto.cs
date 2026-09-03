using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace BeniceSoft.Abp.Sample.Application.Contracts;

/// <summary>
/// 创建用户 DTO
/// 添加 [DataContract] 特性以支持 gRPC/protobuf 序列化
/// </summary>
[DataContract]
public class CreateDto
{
    [Required]
    [DataMember(Order = 1)]
    public string Name { get; set; } = string.Empty;
}
