using BeniceSoft.Abp.Core;
using BeniceSoft.Abp.Swagger.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Volo.Abp.Modularity;

namespace BeniceSoft.Abp.Swagger;

[DependsOn(typeof(BeniceSoftAbpCoreModule))]
public class BeniceSoftAbpSwaggerModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;
        var swaggerOptions = services.ExecutePreConfiguredActions<BeniceSoftSwaggerOptions>();

        services.AddSingleton(Options.Create(swaggerOptions));

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(swaggerOptions.Version, new OpenApiInfo
            {
                Title = swaggerOptions.Title,
                Version = swaggerOptions.Version,
                Description = swaggerOptions.Description,
                Contact = swaggerOptions.Contact,
                License = swaggerOptions.License
            });

            options.DocInclusionPredicate((doc, description) =>
            {
                if (swaggerOptions.HideAbpDefaultEndpoints &&
                    description.RelativePath is not null &&
                    description.RelativePath.StartsWith("api/abp/", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            });

            if (swaggerOptions.UseFullTypeNameAsSchemaId)
            {
                options.CustomSchemaIds(type => type.FullName);
            }

            if (swaggerOptions.AutoLoadXmlComments)
            {
                LoadXmlComments(options, swaggerOptions.XmlCommentsFilter);
            }

            if (swaggerOptions.EnableEnumDescription)
            {
                options.SchemaFilter<EnumDescriptionSchemaFilter>();
            }

            if (swaggerOptions.EnableBearerAuth)
            {
                ConfigureBearerAuth(options, swaggerOptions);
            }

            swaggerOptions.ConfigureSwaggerGen?.Invoke(options);
        });
    }

    /// <summary>
    /// 加载 XML 注释文件
    /// </summary>
    private static void LoadXmlComments(SwaggerGenOptions options, Func<string, bool>? filter)
    {
        var basePath = AppContext.BaseDirectory;
        var xmlFiles = Directory.GetFiles(basePath, "*.xml", SearchOption.TopDirectoryOnly);

        foreach (var xmlFile in xmlFiles)
        {
            var fileName = Path.GetFileName(xmlFile);

            if (filter is not null && !filter(fileName))
            {
                continue;
            }

            try
            {
                options.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);
            }
            catch
            {
                // 忽略无效的 XML 文件
            }
        }
    }

    /// <summary>
    /// 配置 Bearer 认证
    /// </summary>
    private static void ConfigureBearerAuth(SwaggerGenOptions options, BeniceSoftSwaggerOptions swaggerOptions)
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = swaggerOptions.BearerAuthDescription,
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
    }
}

