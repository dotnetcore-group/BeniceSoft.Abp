using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace BeniceSoft.Abp.Swagger;

/// <summary>
/// Swagger 应用程序构建器扩展方法
/// </summary>
public static class SwaggerApplicationBuilderExtensions
{
    public static IApplicationBuilder UseBeniceSoftSwagger(this IApplicationBuilder app)
    {
        return app.UseBeniceSoftSwagger(_ => { });
    }

    public static IApplicationBuilder UseBeniceSoftSwagger(
        this IApplicationBuilder app,
        Action<SwaggerUIOptions> configureOptions)
    {
        var options = app.ApplicationServices.GetRequiredService<IOptions<BeniceSoftSwaggerOptions>>().Value;

        app.UseSwagger();

        // 禁用 Swagger 相关资源的浏览器缓存，避免升级后浏览器使用旧版缓存导致渲染异常
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                    context.Response.Headers.Pragma = "no-cache";
                    context.Response.Headers.Expires = "0";
                    return Task.CompletedTask;
                });
            }

            await next();
        });

        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint(options.SwaggerEndpoint, $"{options.Title} {options.Version}");
            c.RoutePrefix = options.RoutePrefix;
            c.DocExpansion(options.DocExpansion);
            c.DefaultModelsExpandDepth(options.DefaultModelsExpandDepth);

            if (options.EnablePersistAuthorization)
            {
                c.EnablePersistAuthorization();
            }

            if (options.DisplayRequestDuration)
            {
                c.DisplayRequestDuration();
            }

            options.ConfigureSwaggerUI?.Invoke(c);
            configureOptions(c);
        });

        return app;
    }
}

