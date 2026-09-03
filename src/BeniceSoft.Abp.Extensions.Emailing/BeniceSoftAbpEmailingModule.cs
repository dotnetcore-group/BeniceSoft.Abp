using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Emailing;
using Volo.Abp.MailKit;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Extensions.Emailing;

[DependsOn(typeof(AbpMailKitModule))]
public class BeniceSoftAbpEmailingModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var section = configuration.GetSection("Smtp");

        context.Services.Configure<SmtpOptions>(section);
        context.Services.Replace(ServiceDescriptor.Transient<IEmailSender, ConfigurationSmtpEmailSender>());
    }
}
