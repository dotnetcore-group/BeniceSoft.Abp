using System.Reflection;

namespace BeniceSoft.Abp.Extensions.OpenTelemetry;

/// <summary>
/// OpenTelemetry 相关环境变量读取
/// </summary>
public static class BeniceSoftOpenTelemetryEnvironment
{
    public const string ServiceNameVariable = "OTEL_SERVICE_NAME";
    public const string OtlpEndpointVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    /// <summary>
    /// 是否配置了 OTLP 导出地址
    /// </summary>
    public static bool IsOtlpEndpointConfigured =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(OtlpEndpointVariable));

    /// <summary>
    /// 服务名：优先 OTEL_SERVICE_NAME，其次应用名，最后入口程序集名
    /// </summary>
    public static string GetServiceName(string? applicationName = null)
    {
        var serviceName = Environment.GetEnvironmentVariable(ServiceNameVariable);
        if (!string.IsNullOrWhiteSpace(serviceName))
        {
            return serviceName;
        }

        if (!string.IsNullOrWhiteSpace(applicationName))
        {
            return applicationName;
        }

        return Assembly.GetEntryAssembly()?.GetName().Name ?? "unknown_service";
    }
}