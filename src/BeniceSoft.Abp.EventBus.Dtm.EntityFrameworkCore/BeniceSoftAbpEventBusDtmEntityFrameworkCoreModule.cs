using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace BeniceSoft.Abp.EventBus.Dtm.EntityFrameworkCore;

[DependsOn(
    typeof(BeniceSoftAbpEventBusDtmModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class BeniceSoftAbpEventBusDtmEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddTransient<DtmUnitOfWork>();
        context.Services.TryAddTransient<DtmOutboxSender>();
        context.Services.TryAddTransient<DtmInboxProcessor>();

        context.Services.Replace(ServiceDescriptor.Transient<IUnitOfWork, DtmUnitOfWork>());
        context.Services.Replace(ServiceDescriptor.Transient<IOutboxSender, DtmOutboxSender>());
        context.Services.Replace(ServiceDescriptor.Transient<IInboxProcessor, DtmInboxProcessor>());

        context.Services.AddTransient<DtmDbContextEventOutbox>();
        context.Services.AddTransient<DtmDbContextEventInbox>();

        context.Services.AddTransient(typeof(IDtmDbContextEventOutbox<>), typeof(DtmDbContextEventOutbox<>));
        context.Services.AddTransient(typeof(IDtmDbContextEventInbox<>), typeof(DtmDbContextEventInbox<>));
    }

    public override void PostConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration().GetSection("EventBus");
        var logger = context.Services.GetInitLogger<BeniceSoftAbpEventBusDtmEntityFrameworkCoreModule>();

        var dbContextName = configuration["DTM:DbContextName"] ?? "";
        var dbContextType = context.Services
            .Where(x => x.ServiceType is { IsClass: true, IsAbstract: false })
            .Select(x => x.ServiceType)
            .FirstOrDefault(t => t.Name == dbContextName || t.FullName?.EndsWith($".{dbContextName}") == true);

        if (dbContextType == null)
        {
            dbContextType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.Name == dbContextName && typeof(Volo.Abp.EntityFrameworkCore.IEfCoreDbContext).IsAssignableFrom(t));
        }

        if (dbContextType == null)
        {
            logger.LogWarning("未找到 DTM:DbContextName 对应的 DbContext 类型，已跳过 MSG/TCC/SAGA 统一 DbContext 注入配置。DbContextName={DbContextName}", dbContextName);
            return;
        }

        context.Services.UseDbContextWithDtmTransactions(dbContextType);
    }

}
