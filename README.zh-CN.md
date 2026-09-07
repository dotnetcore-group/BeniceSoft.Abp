# BeniceSoft.Abp

基于 [ABP](https://abp.io/) 的企业级 .NET 框架。提供 DDD、EF Core（Bulk / QueryFuture / 分库分表）、认证与数据权限、缓存、分布式锁、限流、动态查询、操作日志、变更审计、DTM 事件总线、RabbitMQ、Redis、Swagger、服务发现、Excel/PDF 等基础设施。

English: [README.md](./README.md)

## 技术栈

| 项 | 版本 |
|------|---------|
| 包版本（`common.props`） | `10.0.16-dev` |
| 目标框架 | .NET 10 |
| ABP（Volo.Abp） | 10.6.0 |
| EF Core / ASP.NET Core | 10.0.10 |
| Redis | StackExchange.Redis |
| 认证 | OpenIddict Claims |
| DTM | Dtmcli / Dtmgrpc 1.4.0 |

## 目录结构

```
src/       # 框架包
samples/   # Host 样例（Bulk / QueryFuture / 分片 API）
tests/     # 单元与集成测试
docs/      # 架构与接入文档
```

## 文档

| 文档 | 说明 |
|------|------|
| [docs/GRPC-SDK-ARCHITECTURE.md](./docs/GRPC-SDK-ARCHITECTURE.md) | gRPC Sdk / 编译期 proto |
| [docs/分库分表使用指南.md](./docs/分库分表使用指南.md) | 分库分表接入（WarehouseCenter 举例） |

## 类库

### 核心

| 包 | 说明 |
|---------|--------|
| **BeniceSoft.Core** | 无 ABP 依赖。`ResponseResult<T>`、动态查询模型（`IDynamicQueryRequest`、`ExprOperator`）、雪花 ID、反射器、DeepClone/ShallowClone、FluentClient、扩展方法 |
| **BeniceSoft.Abp.Core** | `BeniceSoftAbpCoreModule`。`IBeniceSoftCurrentUser`、`IgnoreJsonFormat` / `IgnoreBind` / `FillBasicDataValue`、HTTP 常量、异常类型 |

### DDD

| 包 | 说明 |
|---------|--------|
| **BeniceSoft.Abp.Ddd.Domain** | 审计实体（`DateTimeOffset` + `long`）、`[AuditTracked]`、`IQueryableWrapper` / 工厂、`ISqlExecuter` |
| **BeniceSoft.Abp.Ddd.Application.Contracts** | 应用层契约 |
| **BeniceSoft.Abp.Ddd.Application** | `BeniceSoftApplicationService` + `IQueryableWrapperFactory` |

### 数据访问

| 包 | 说明 |
|---------|--------|
| **BeniceSoft.Abp.EntityFrameworkCore** | `BeniceSoftAbpDbContext`、`EfCoreQueryableWrapper`、`DapperSqlExecuter`、雪花值生成器、命名约定、**ForceSaveChange**、**QueryFuture**、Bulk 抽象、SaveChanges 时采集变更审计 |
| **BeniceSoft.Abp.EntityFrameworkCore.Sharding** | 完整分片引擎：谓词路由、`AsRoute` / `UseMerge` / `AsSequence`、流式合并、读写分离、Compensate 建表；对接 ABP 壳 DbContext / UoW / `IRepository` |
| **BeniceSoft.Abp.EntityFrameworkCore.SqlServer** | Bulk（SqlBulkCopy + MERGE）、Sequence（`NEXT VALUE FOR`）、Hint（`WITH (NOLOCK)` 等） |
| **BeniceSoft.Abp.EntityFrameworkCore.PostgreSql** | Bulk（COPY + UPDATE/DELETE/ON CONFLICT）、Sequence（`NEXTVAL`）、Hint（`FOR UPDATE` / `FOR SHARE`） |

### 认证与多租户

| 包 | 说明 |
|---------|--------|
| **BeniceSoft.Abp.Auth.Core** | `IUserPermission`、行/字段权限模型、`[FieldAuth]` |
| **BeniceSoft.Abp.Auth.Repository** | `IRowPermissionRepository`、行权限谓词构建 |
| **BeniceSoft.Abp.Auth.EntityFrameworkCore** | `RowPermissionEfCoreRepository`、`AddRowPermissionRepositories<TDbContext>()`、字段权限 SaveChanges 拦截器 |
| **BeniceSoft.Abp.Auth** | `BeniceSoftCurrentUser`（OpenIddict Claims）、权限中间件、`[FunctionPermission]`、字段权限过滤器 |
| **BeniceSoft.Abp.MultiTenancy** | 从 `IBeniceSoftCurrentUser.TenantId` 解析租户；透传 `ITenantStore`（由 AspNetCore 模块引入） |

### ASP.NET Core / HTTP / 服务发现

| 包 | 说明 |
|---------|--------|
| **BeniceSoft.Abp.AspNetCore** | `JsonFormatResponseFilter` → `ResponseResult<T>`、异常中间件、文化映射 |
| **BeniceSoft.Abp.Http.Client** | 代理 HTTP 客户端（转发 Authorization）、机器账号 `client_credentials` Token |
| **BeniceSoft.Abp.ServiceDiscovery** | `AddHttpServiceDiscovery` — 注册 / 健康检查 / 元数据（HTTP 或 Redis） |
| **BeniceSoft.Abp.Swagger** | XML 注释、枚举 Description 过滤器、Bearer、持久化授权 |
| **BeniceSoft.OAuth.DingTalk** | 钉钉 OAuth（扫码 + 微应用免登） |

### 缓存 / 锁 / 限流 / Redis

| 包 | 说明 |
|---------|--------|
| **…Caching.Abstractions** | `[Cacheable]`（Key / Condition / Unless / ExpirationSeconds） |
| **…Caching** | AOP 拦截；前缀与默认过期来自 `BeniceSoft:Caching` |
| **…Caching.MessagePack** / **…SystemTextJson** | 值序列化器 |
| **…DistributedLock.Abstractions** | `[DistributedLock]`、`IDistributedLockProvider` |
| **…DistributedLock** | Redis Lua 锁 + 自动续期（非 RedLock.net） |
| **…RateLimiting.Abstractions** | `[RateLimit]` — Ip / UserId / TenantId / Custom / Global |
| **…RateLimiting** | Redis 令牌桶 AOP（配置节 `RateLimiting`） |
| **…Redis** | 命令封装、Lua 锁、Pub/Sub |

### 动态查询

| 包 | 说明 |
|---------|--------|
| *（模型在 **BeniceSoft.Core**）* | `IDynamicQueryRequest`、条件组、操作符 |
| **…DynamicQuery.EfCore** | 请求 → LINQ 表达式 |
| **…DynamicQuery.Sql** | SqlKata → 参数化 SQL（SqlServer / PostgreSQL / MySQL 等） |

### 消息与分布式事务

| 包 | 说明 |
|---------|--------|
| **…Extensions.RabbitMQ** | Work / Fanout / Direct / Topic 发布与消费（复用 ABP 连接池） |
| **…EventBus.Dtm** | DTM outbox / inbox / UoW |
| **…EventBus.Dtm.Http** | Dtmcli HTTP、TCC / SAGA（`IGlobalTransaction`）、`[DtmBranch]`、`UseDtmHttpMiddleware()` |
| **…EventBus.Dtm.EntityFrameworkCore** | EF outbox/inbox + 屏障（MySQL / PG / SQL Server / SQLite） |

### 日志与审计

| 包 | 说明 |
|---------|--------|
| **…OperationLogging.Abstractions** | `[OperationLog]`、`OperationLogContext` |
| **…OperationLogging** | AOP 拦截器 |
| **…OperationLogging.EventBus** / **…Redis** | 经分布式事件总线或 Redis Pub/Sub 分发 |
| **…AuditTrail.Abstractions** | `EntityChangeRecord`、`IEntityChangeDispatcher`、配置项 |
| **…AuditTrail.EventBus** | 发布 `EntityChangeEvent`（领域 `[AuditTracked]`；EF DbContext 采集） |

### 其他

| 包 | 说明 |
|---------|--------|
| **…Extensions.Emailing** | MailKit SMTP `IEmailSender`（配置节 `Smtp`） |
| **BeniceSoft.Office.Excel** | NPOI 导入导出（`ExcelMapper`）、`[ExcelColumn]`、模板 `ExcelReport` — 无 AbpModule |
| **BeniceSoft.Office.Pdf** | 解析文本/表单域/条码/PNG 页（`Pdf` / `IPdfParser`） — 无 AbpModule |

## Host 示意

```csharp
[DependsOn(
    typeof(BeniceSoftAbpAspNetCoreModule),
    typeof(BeniceSoftAbpSwaggerModule),
    typeof(BeniceSoftAbpAuthModule),
    typeof(BeniceSoftAbpDistributedLockModule),
    typeof(BeniceSoftAbpCachingMessagePackModule),
    typeof(BeniceSoftAbpRateLimitingModule),       // 可选
    typeof(BeniceSoftAbpOperationLoggingEventBusModule),
    typeof(BeniceSoftAbpAuditTrailEventBusModule),
    typeof(AbpAutofacModule),                      // AOP 需要
    typeof(YourApplicationModule),
    typeof(YourEntityFrameworkCoreModule)
)]
public class YourHostModule : AbpModule
{
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        app.UseCorrelationId();
        app.UseRouting();
        app.UseBeniceSoftExceptionHandlingMiddleware();
        app.UseAbpRequestLocalization();
        app.UseBeniceSoftAuthentication();
        app.UseBeniceSoftAuthorization();
        app.UseBeniceSoftUserPermission();
        app.UseBeniceSoftSwagger();
        app.UseConfiguredEndpoints();
    }
}
```

声明式 AOP（需 `virtual` + Autofac）：`[Cacheable]`、`[DistributedLock]`、`[RateLimit]`、`[OperationLog]`。

EF 提供程序：依赖 **PostgreSql** 或 **SqlServer** 模块。分库分表见 [docs/分库分表使用指南.md](./docs/分库分表使用指南.md)。样例 API：`samples/BeniceSoft.Abp.Sample.Host`。

```bash
dotnet build BeniceSoft.Abp.sln
cd samples/BeniceSoft.Abp.Sample.Host && dotnet run
```

## 许可证

MIT
