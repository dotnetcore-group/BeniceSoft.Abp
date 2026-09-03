using BeniceSoft.Core;
using BeniceSoft.Core.Strategy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BeniceSoft.Abp.Sample.EntityFrameworkCore;

/// <summary>
/// 设计时生成 Migration 用（不含 UseSharding；迁移描述逻辑模型）。
/// </summary>
public sealed class SampleDbContextFactory : IDesignTimeDbContextFactory<SampleDbContext>
{
    public SampleDbContext CreateDbContext(string[] args)
    {
        // OnModelCreating 里 HasValueGenerator<SnowDateIdGenerator> 会读 Singleton，设计时必须先赋值
        Singleton<SnowDateIdGenerator>.Instance ??= new SnowDateIdGenerator();

        var configuration = BuildConfiguration();
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "未找到 ConnectionStrings:Default。请确认 Host 项目的 appsettings.json / appsettings.Development.json 已配置连接串。");
        }

        var builder = new DbContextOptionsBuilder<SampleDbContext>()
            .UseNpgsql(connectionString);

        return new SampleDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var hostPath = ResolveHostPath();
        var environment =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        return new ConfigurationBuilder()
            .SetBasePath(hostPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string ResolveHostPath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "../BeniceSoft.Abp.Sample.Host/"),
            Path.Combine(Directory.GetCurrentDirectory(), "BeniceSoft.Abp/samples/BeniceSoft.Abp.Sample.Host/"),
            Path.Combine(Directory.GetCurrentDirectory(), "samples/BeniceSoft.Abp.Sample.Host/"),
            Directory.GetCurrentDirectory(),
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(full, "appsettings.json")))
            {
                return full;
            }
        }

        throw new InvalidOperationException(
            "无法定位 BeniceSoft.Abp.Sample.Host 目录（缺少 appsettings.json）。请从 EF / Host / 解决方案根目录执行 Update-Database。");
    }
}
