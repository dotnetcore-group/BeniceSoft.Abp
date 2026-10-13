using BeniceSoft.Abp.Extensions.OpenTelemetry;
using Serilog.Configuration;

namespace Serilog;

public static class OpenTelemetryLoggerSinkConfigurationExtensions
{
    public static LoggerConfiguration OtlpIfConfigured(this LoggerSinkConfiguration writeTo, string? applicationName = null)
    {
        ArgumentNullException.ThrowIfNull(writeTo);

        if (!BeniceSoftOpenTelemetryEnvironment.IsOtlpEndpointConfigured)
        {
            return writeTo.Sink(new NullSink());
        }

        var serviceName = BeniceSoftOpenTelemetryEnvironment.GetServiceName(applicationName);
        return writeTo.OpenTelemetry(options =>
        {
            options.ResourceAttributes["service.name"] = serviceName;
        });
    }

    private sealed class NullSink : Serilog.Core.ILogEventSink
    {
        public void Emit(Serilog.Events.LogEvent logEvent)
        {
        }
    }
}