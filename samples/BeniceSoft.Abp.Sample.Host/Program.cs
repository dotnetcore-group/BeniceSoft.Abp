using System.Text;
using BeniceSoft.Core;
using BeniceSoft.Core.Strategy;
using Serilog;
using Serilog.Events;
using Winton.Extensions.Configuration.Consul;

namespace BeniceSoft.Abp.Sample.Host;

public class Program
{
    public static async Task Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);
        Singleton<SnowDateIdGenerator>.Instance = new SnowDateIdGenerator();

        var configuration = new ConfigurationBuilder()
            //.AddYamlFile("appsettings.Development.yaml")
            .AddJsonFile("appsettings.json")
            .Build();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .ReadFrom.Configuration(configuration)
            .WriteTo.Console()
            .CreateLogger();

        try
        {
            Log.Information("Starting web host.");
            var builder = WebApplication.CreateBuilder(args);

            var env = builder.Environment.EnvironmentName;
            builder.Configuration.AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true);

            var consulAddress = builder.Configuration["Consul:Address"];
            var profileList = builder.Configuration.GetSection("Consul:ProFileList").Get<List<string>>();

            if (!string.IsNullOrEmpty(consulAddress) && profileList?.Count == 0)
            {
                foreach (var profile in profileList)
                {
                    builder.Configuration.AddConsul(profile, options =>
                    {
                        options.ConsulConfigurationOptions = consul =>
                        {
                            consul.Address = new Uri(consulAddress);
                        };
                        options.Optional = true;
                        options.ReloadOnChange = true;
                        //options.Parser = new YamlConsulConfigurationParser(); // 如果配置文件是yaml格式，则需要使用此解析器
                    });
                }
            }

            builder.Host
                //.UseAgileConfig(e => Log.Logger.Debug($"configs {e.Action}"))
                .AddAppSettingsSecretsJson()
                .UseAutofac()
                .UseSerilog();
            await builder.AddApplicationAsync<SampleHostModule>();
            var app = builder.Build();
            await app.InitializeApplicationAsync();
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly!");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}