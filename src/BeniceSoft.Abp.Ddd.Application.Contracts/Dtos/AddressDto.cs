namespace BeniceSoft.Abp.Ddd.Application.Contracts;

public class AddressDto
{
    /// <summary>
    /// 二位国家编码
    /// </summary>
    public string CountryCode { get; set; } = string.Empty;

    /// <summary>
    /// 国家名称
    /// </summary>
    public string CountryName { get; set; } = string.Empty;

    /// <summary>
    /// 省/州
    /// </summary>
    public string Province { get; set; } = string.Empty;

    /// <summary>
    /// 市
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// 区/县
    /// </summary>
    public string District { get; set; } = string.Empty;

    /// <summary>
    /// 街道
    /// </summary>
    public string Street { get; set; } = string.Empty;

    /// <summary>
    /// 邮政编码
    /// </summary>
    public string ZipCode { get; set; } = string.Empty;

    /// <summary>
    /// 行政区域（省/市/区县/街道）
    /// </summary>
    public string AdministrativeDivision { get; set; } = string.Empty;

    /// <summary>
    /// 详细地址
    /// </summary>
    public string DetailedAddress { get; set; } = string.Empty;

    public override string ToString()
    {
        return string.Join('/', AdministrativeDivision, DetailedAddress);
    }
}
