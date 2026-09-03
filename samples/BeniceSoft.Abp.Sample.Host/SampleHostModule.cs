using BeniceSoft.Abp.AspNetCore;
using BeniceSoft.Abp.AspNetCore.Extensions;
using BeniceSoft.Abp.AspNetCore.Middlewares;
using BeniceSoft.Abp.Auth;
using BeniceSoft.Abp.Auth.Extensions;
using BeniceSoft.Abp.EntityFrameworkCore.Sharding;
using BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;
using BeniceSoft.Abp.EventBus.Dtm.Http;
using BeniceSoft.Abp.Extensions.Caching.MessagePack;
using BeniceSoft.Abp.Extensions.DistributedLock;
using BeniceSoft.Abp.Extensions.RabbitMQ;
using BeniceSoft.Abp.Extensions.RateLimiting;
using BeniceSoft.Abp.OperationLogging.EventBus;
using BeniceSoft.Abp.Sample.Application;
using BeniceSoft.Abp.Sample.Application.Services;
using BeniceSoft.Abp.Sample.EntityFrameworkCore;
using BeniceSoft.Abp.Sample.Host.RabbitMqDemo;
using BeniceSoft.Abp.Sample.RemoteService.Implements;
using BeniceSoft.Abp.ServiceDiscovery;
using BeniceSoft.Abp.Swagger;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Logging;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace BeniceSoft.Abp.Sample.Host;

[DependsOn(
    typeof(BeniceSoftAbpAspNetCoreModule),
    typeof(BeniceSoftAbpSwaggerModule),
    typeof(BeniceSoftAbpDistributedLockModule),
    typeof(BeniceSoftAbpRateLimitingModule),
    typeof(BeniceSoftAbpCachingMessagePackModule),
    typeof(BeniceSoftAbpAuthModule),
    typeof(BeniceSoftAbpOperationLoggingEventBusModule),
    typeof(BeniceSoftAbpEventBusDtmHttpModule),
    typeof(BeniceSoftAbpEventBusDtmEntityFrameworkCoreModule),
    typeof(BeniceSoftAbpRabbitMqModule),
    typeof(RemoteServiceModule),
    typeof(SampleApplicationModule),
    typeof(SampleEntityFrameworkCoreModule)
)]
public class SampleHostModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.PreConfigure<BeniceSoftSwaggerOptions>(options =>
        {
            options.Title = "Wecharmer Sample API";
            options.Version = "v1";
            options.Description = "Wecharmer Sample API 文档";
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        IdentityModelEventSource.ShowPII = true;
        context.Services.AddHttpClient();

        // 逻辑多租户：租户来自认证 claim（AM 维护），不走配置文件 / ABP 租户表；物理分库由 Sharding 负责。
        Configure<AbpMultiTenancyOptions>(options => { options.IsEnabled = true; });

        // 大文件上传：默认 Form ~128MB、Kestrel ~30MB，未进 Action 就会被拦
        context.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = PdfSampleAppService.MaxUploadBytes;
            options.ValueLengthLimit = int.MaxValue;
            options.MultipartHeadersLengthLimit = int.MaxValue;
        });
        context.Services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = PdfSampleAppService.MaxUploadBytes;
        });
        context.Services.Configure<IISServerOptions>(options =>
        {
            options.MaxRequestBodySize = PdfSampleAppService.MaxUploadBytes;
        });

        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options
                .ConventionalControllers
                .Create(typeof(SampleApplicationModule).Assembly, opts =>
                {
                    opts.RootPath = "sample";
                });
        });

        Configure<AbpAntiForgeryOptions>(options =>
        {
            options.TokenCookie.Expiration = TimeSpan.Zero;
            options.AutoValidate = false;
        });

        Configure<AbpClockOptions>(options => { options.Kind = DateTimeKind.Utc; });

        context.Services.AddBeniceSoftAuthentication();
        context.Services.AddBeniceSoftAuthorization();

        // 作业队列消费者
        context.Services.AddRabbitWorkConsumer<AmazonOrderBatchMessage, AmazonOrderHandler>(options =>
        {
            options.QueueName = "order.amazon.import";
            options.PrefetchCount = 10;
        });
        context.Services.AddRabbitWorkConsumer<WarfireOrderBatchMessage, WarfireOrderHandler>(options =>
        {
            options.QueueName = "order.warfire.import";
            options.PrefetchCount = 5;
        });

        context.Services.AddHttpServiceDiscovery(options =>
        {
            configuration.GetSection("ServiceDiscovery").Bind(options);
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // 建表规范：① Migration 先落物理表 ② 再 Compensate 建分表物理表。顺序不可反,实际业务中不使用代码执行自动迁移，都需要手工生成sql脚本执行迁移
        //using (var scope = context.ServiceProvider.CreateScope())
        //{
        //    var db = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        //    db.Database.Migrate();
        //}

        context.ServiceProvider.UseCompensate();

        var app = context.GetApplicationBuilder();

        app.UseCorrelationId();

        // 路由
        app.UseRouting();

        // 跨域
        app.UseCors(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

        app.UseBeniceSoftExceptionHandlingMiddleware();

        app.UseAbpRequestLocalization();

        // DTM回调中间件，必须放在认证授权之前，以确保回调请求能够正确处理，不受认证授权的影响
        app.UseDtmHttpMiddleware();

        // 身份验证
        app.UseBeniceSoftAuthentication();

        // 多租户解析（默认支持 Header/Cookie/Query：__tenant）
        app.UseMultiTenancy();

        // 认证授权
        app.UseBeniceSoftAuthorization();

        // 用户权限
        //app.UseBeniceSoftUserPermission();

        // 使用 BeniceSoft Swagger
        app.UseBeniceSoftSwagger();

        app.UseConfiguredEndpoints(endpoints =>
        {
            endpoints.MapServiceDiscoveryHealthCheck();
        });
    }
}
