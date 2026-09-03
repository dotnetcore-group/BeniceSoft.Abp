using Winton.Extensions.Configuration.Consul.Parsers;

namespace BeniceSoft.Abp.Sample.Host;

/// <summary>
/// consul配置文件yaml格式解析器
/// </summary>
public class YamlConsulConfigurationParser : IConfigurationParser
{
    public IDictionary<string, string> Parse(Stream stream)
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddYamlStream(stream);
        var config = configBuilder.Build();

        var result = new Dictionary<string, string>();
        foreach (var kvp in config.AsEnumerable())
        {
            if (kvp.Value != null)
            {
                result[kvp.Key] = kvp.Value;
            }
        }
        return result;
    }

}
