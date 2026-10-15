using Volo.Abp.Domain.Values;

namespace BeniceSoft.Abp.Ddd.Domain.ValueObjects;

public sealed class Address : ValueObject, IEquatable<Address>
{
    public static readonly Address Default = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty);

    /// <summary>
    /// 二位国家编码
    /// </summary>
    public string CountryCode { get; private set; }

    /// <summary>
    /// 国家名称
    /// </summary>
    public string CountryName { get; private set; }

    /// <summary>
    /// 省/州
    /// </summary>
    public string Province { get; private set; }

    /// <summary>
    /// 市
    /// </summary>
    public string City { get; private set; }

    /// <summary>
    /// 区/县
    /// </summary>
    public string District { get; private set; }

    /// <summary>
    /// 街道
    /// </summary>
    public string Street { get; private set; }

    /// <summary>
    /// 邮政编码
    /// </summary>
    public string ZipCode { get; private set; }

    /// <summary>
    /// 行政区域（省/市/区县/街道）
    /// </summary>
    public string AdministrativeDivision { get; private set; }

    /// <summary>
    /// 详细地址
    /// </summary>
    public string DetailedAddress { get; private set; }


    public Address(
        string countryCode,
        string countryName,
        string province,
        string city,
        string district,
        string street,
        string zipCode,
        string administrativeDivision,
        string detailedAddress)
    {
        CountryCode = countryCode;
        CountryName = countryName;
        Province = province;
        City = city;
        District = district;
        Street = street;
        ZipCode = zipCode;
        DetailedAddress = detailedAddress;
        AdministrativeDivision = administrativeDivision;
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return CountryCode;
        yield return CountryName;
        yield return Province;
        yield return City;
        yield return District;
        yield return Street;
        yield return ZipCode;
        yield return AdministrativeDivision;
        yield return DetailedAddress;
    }

    public bool Equals(Address? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return CountryCode == other.CountryCode &&
            CountryName == other.CountryName &&
            Province == other.Province &&
            City == other.City &&
            District == other.District &&
            Street == other.Street &&
            ZipCode == other.ZipCode &&
            AdministrativeDivision == other.AdministrativeDivision &&
            DetailedAddress == other.DetailedAddress;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        return Equals((Address)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(CountryCode, CountryName, Province, City, District, Street, ZipCode, DetailedAddress);
    }

    public override string ToString()
    {
        return $"{AdministrativeDivision} {DetailedAddress}";
    }
}
