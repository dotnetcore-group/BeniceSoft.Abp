using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace BeniceSoft.Abp.Swagger;

/// <summary>
/// BeniceSoft Swagger 配置选项
/// </summary>
public class BeniceSoftSwaggerOptions
{
    /// <summary>
    /// API 标题
    /// </summary>
    public string Title { get; set; } = "API";

    private string _version = "v1";
    private string? _swaggerEndpoint;

    /// <summary>
    /// API 版本
    /// </summary>
    public string Version
    {
        get => _version;
        set
        {
            _version = value;

            if (_swaggerEndpoint == null)
            {
                _swaggerEndpoint = $"/swagger/{_version}/swagger.json";
            }
        }
    }

    /// <summary>
    /// API 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否启用 Bearer 认证配置
    /// </summary>
    public bool EnableBearerAuth { get; set; } = true;

    /// <summary>
    /// 是否启用枚举描述（将 Description/Display 特性添加到 Swagger 文档）
    /// </summary>
    public bool EnableEnumDescription { get; set; } = true;

    /// <summary>
    /// 是否使用完整类名作为 SchemaId
    /// </summary>
    public bool UseFullTypeNameAsSchemaId { get; set; } = true;

    /// <summary>
    /// 是否自动加载 XML 注释文件
    /// </summary>
    public bool AutoLoadXmlComments { get; set; } = true;

    /// <summary>
    /// XML 注释文件过滤器（返回 true 表示包含该文件）
    /// </summary>
    public Func<string, bool>? XmlCommentsFilter { get; set; }

    /// <summary>
    /// Swagger 文档端点路径（默认：/swagger/{Version}/swagger.json）
    /// 如果手动设置此属性，则不会随 Version 自动更新
    /// </summary>
    public string SwaggerEndpoint
    {
        get => _swaggerEndpoint ?? $"/swagger/{_version}/swagger.json";
        set => _swaggerEndpoint = value;
    }

    /// <summary>
    /// SwaggerUI 路由前缀（默认：swagger）
    /// </summary>
    public string RoutePrefix { get; set; } = "swagger";

    /// <summary>
    /// 是否默认折叠所有操作
    /// </summary>
    public DocExpansion DocExpansion { get; set; } = DocExpansion.None;

    /// <summary>
    /// 是否隐藏 Models 区域（-1 表示隐藏）
    /// </summary>
    public int DefaultModelsExpandDepth { get; set; } = -1;

    /// <summary>
    /// 是否启用持久化授权（刷新页面后保留 Token）
    /// </summary>
    public bool EnablePersistAuthorization { get; set; } = true;

    /// <summary>
    /// 是否显示请求耗时
    /// </summary>
    public bool DisplayRequestDuration { get; set; } = true;

    /// <summary>
    /// 是否隐藏 ABP 框架默认端点（如 /api/abp/api-definition 等），默认隐藏
    /// </summary>
    public bool HideAbpDefaultEndpoints { get; set; } = true;

    /// <summary>
    /// 自定义 SwaggerGen 配置
    /// </summary>
    public Action<SwaggerGenOptions>? ConfigureSwaggerGen { get; set; }

    /// <summary>
    /// 自定义 SwaggerUI 配置
    /// </summary>
    public Action<SwaggerUIOptions>? ConfigureSwaggerUI { get; set; }

    /// <summary>
    /// Bearer 认证描述
    /// </summary>
    public string BearerAuthDescription { get; set; } = "请输入 JWT Token（不要带 Bearer 前缀）";

    /// <summary>
    /// 联系人信息
    /// </summary>
    public OpenApiContact? Contact { get; set; }

    /// <summary>
    /// 许可证信息
    /// </summary>
    public OpenApiLicense? License { get; set; }
}

