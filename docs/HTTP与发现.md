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

### 进程启动编号

`ServiceInstance.StartupId` 是注册请求顶层的可空 `Guid`，JSON 字段为 `startupId`。自动注册后台服务在同一进程中生成一次非空编号，首次注册、失败重试、周期心跳及断线后的重新注册始终复用它；业务进程重启才生成新编号。编号无需配置，也不使用机器名称、地址或版本号推导。

HTTP 注册继续使用 `POST api/service-discovery/register`，现有 `PostAsJsonAsync` 会随请求发送该字段。旧客户端缺少此字段时为 `null`，DTO 本身不生成编号，接收方不能把缺失值当成一次新启动。直接调用 `IServiceRegistry` 的自定义调用方应自行保持编号的进程生命周期，不能在每次请求时重新生成。

服务实例的稳定定位仍为服务名和规范化地址。`StartupId` 独立于 `Metadata`，不得放入 `Metadata.Custom`、用作路由实例主键或参与路由配置变更比较。后续网关应在 Redis 记录启动编号、心跳时间和文档刷新状态：相同编号只续期；新编号登记 Swagger 刷新需求；刷新成功才记为已处理，失败保留重试状态。多实例时需核对被刷新实例及启动编号，避免读取旧实例文档却标记新启动已完成。

当前注册版本通常保持默认 `1.0.0`，增删接口并不会自动修改它；`ApiVersion` 和 Swagger 的 `info.version` 也不能当作文档内容修订号。网关已有的版本分组和缓存地址可以保留兼容，但不能用“版本号未变”跳过重启后的文档刷新，不新增人工维护的文档版本要求。

多副本的启动编号各不相同，不能把每个新编号直接等同于一次立即刷新。网关后续按服务（集群）合并短时间内的刷新需求，同一服务串行处理，从触发本轮刷新且当前可用的实例拉取 Swagger，比较文档内容摘要；内容相同则不重复替换缓存。摘要由内容计算，不是注册版本号，也不需要用户配置。拉取期间若又收到新启动需求，须保留到下一轮，不能被本轮完成状态覆盖。

同一合并窗口内的五个副本启动可以共用一次文档拉取；跨窗口滚动启动仍可能再次拉取，但不因此更新路由数据库或触发 YARP 重载。内容摘要只能在取到文档后判断是否变化，不能提前证明所有副本接口一致；新旧程序并存时也不能仅凭最后启动时间认定哪个副本代码最新。因此本方案不承诺任意滚动发布都严格只拉取一次，也不把一次实例刷新记作所有副本内容已核实。

历史依据：`1d9128e`（2026-10-13）让周期心跳复用注册接口；`26f6e17`（2026-10-23）移除了旧 `InstanceId` 及其启动生成逻辑。本次补回进程启动识别能力，保留当前按地址认实例和超时清理的机制。

2026-11-04 本轮仅修改框架源码和本说明，未构建、测试或发包。由维护者发包并确认版本后，再统一升级后端基础包引用，随后实现网关 Redis 启动识别和 Swagger 刷新；当前不代表网关已具备该刷新能力。
