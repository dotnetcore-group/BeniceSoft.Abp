# HTTP 与发现

## 远程 HTTP

模块：`BeniceSoftAbpHttpClientModule`。替换 ABP `IProxyHttpClientFactory`：

- 转发当前请求的 `Authorization`
- 设置远程调用头，并带 `X-Ignore-Json-Format`
- 对端异常为 ABP `RemoteServiceErrorResponse`

```json
"RemoteServices": {
  "Wecharmer.AM": { "BaseUrl": "http://localhost:6002/" }
}
```

无当前用户时使用 `Auth:ClientId`、`ClientSecret`、`Authority` 换 token，地址 `{Authority}/connect/token`。

对端需注册 `UseBeniceSoftExceptionHandlingMiddleware`。请求带远程调用头时，异常不转为 `ResponseResult`。

## Swagger

`DependsOn(typeof(BeniceSoftAbpSwaggerModule))`，管道调用 `UseBeniceSoftSwagger()`。

```csharp
context.Services.PreConfigure<BeniceSoftSwaggerOptions>(options =>
{
    options.Title = "Warehouse API";
    options.Version = "v1";
});
```

包含 XML 注释、枚举 Description、Bearer、持久化授权。

## 服务发现

```csharp
context.Services.AddHttpServiceDiscovery(options =>
{
    configuration.GetSection("ServiceDiscovery").Bind(options);
});
```

```json
"ServiceDiscovery": {
  "ServiceName": "warehouse-center",
  "GatewayBaseUrl": "http://gateway:5188",
  "Metadata": {
    "Version": "1.0.0",
    "ApiVersion": "v1"
  }
}
```

`ServiceName` 必填。未设置 `Address` 时按本机检测。`Version` 为部署版本，`ApiVersion` 为 API 版本。

健康检查：`endpoints.MapServiceDiscoveryHealthCheck()`。经网关的 DTM 分支，`[DtmBranch].ServiceName` 与 `ServiceName` 相同。
