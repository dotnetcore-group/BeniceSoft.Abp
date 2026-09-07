# BeniceSoft.Abp

Enterprise .NET framework on [ABP](https://abp.io/). Shared infrastructure for DDD, EF Core (Bulk / QueryFuture / Sharding), auth & data permission, caching, distributed lock, rate limiting, dynamic query, operation logging, audit trail, DTM event bus, RabbitMQ, Redis, Swagger, service discovery, Excel/PDF.

Chinese: [README.zh-CN.md](./README.zh-CN.md)

## Stack

| Item | Version |
|------|---------|
| Package (`common.props`) | `10.0.16-dev` |
| Target | .NET 10 |
| ABP (Volo.Abp) | 10.6.0 |
| EF Core / ASP.NET Core | 10.0.10 |
| Redis | StackExchange.Redis |
| Auth | OpenIddict claims |
| DTM | Dtmcli / Dtmgrpc 1.4.0 |

## Layout

```
src/       # framework packages
samples/   # Host sample (Bulk / QueryFuture / Sharding APIs)
tests/     # unit & integration tests
docs/      # architecture & how-tos
```

## Docs

| Doc | Topic |
|-----|--------|
| [docs/GRPC-SDK-ARCHITECTURE.md](./docs/GRPC-SDK-ARCHITECTURE.md) | gRPC Sdk / compile-time proto |
| [docs/分库分表使用指南.md](./docs/分库分表使用指南.md) | Sharding integration (WarehouseCenter example) |

## Packages

### Core

| Package | Notes |
|---------|--------|
| **BeniceSoft.Core** | No ABP. `ResponseResult<T>`, DynamicQuery models (`IDynamicQueryRequest`, `ExprOperator`), snowflake IDs, reflector, DeepClone/ShallowClone, FluentClient, helpers |
| **BeniceSoft.Abp.Core** | `BeniceSoftAbpCoreModule`. `IBeniceSoftCurrentUser`, `IgnoreJsonFormat` / `IgnoreBind` / `FillBasicDataValue`, HTTP constants, exceptions |

### DDD

| Package | Notes |
|---------|--------|
| **BeniceSoft.Abp.Ddd.Domain** | Audited entities (`DateTimeOffset` + `long`), `[AuditTracked]`, `IQueryableWrapper` / factory, `ISqlExecuter` |
| **BeniceSoft.Abp.Ddd.Application.Contracts** | Application contracts |
| **BeniceSoft.Abp.Ddd.Application** | `BeniceSoftApplicationService` + `IQueryableWrapperFactory` |

### Data access

| Package | Notes |
|---------|--------|
| **BeniceSoft.Abp.EntityFrameworkCore** | `BeniceSoftAbpDbContext`, `EfCoreQueryableWrapper`, `DapperSqlExecuter`, snowflake value generator, naming conventions, **ForceSaveChange**, **QueryFuture**, Bulk abstractions, audit-trail capture on SaveChanges |
| **BeniceSoft.Abp.EntityFrameworkCore.Sharding** | Full shard engine: predicate route, `AsRoute` / `UseMerge` / `AsSequence`, stream merge, R/W split, compensate tables; ABP shell DbContext / UoW / `IRepository` |
| **BeniceSoft.Abp.EntityFrameworkCore.SqlServer** | Bulk (SqlBulkCopy + MERGE), Sequence (`NEXT VALUE FOR`), Hint (`WITH (NOLOCK)`, …) |
| **BeniceSoft.Abp.EntityFrameworkCore.PostgreSql** | Bulk (COPY + UPDATE/DELETE/ON CONFLICT), Sequence (`NEXTVAL`), Hint (`FOR UPDATE` / `FOR SHARE`) |

### Auth & multi-tenancy

| Package | Notes |
|---------|--------|
| **BeniceSoft.Abp.Auth.Core** | `IUserPermission`, row/field permission models, `[FieldAuth]` |
| **BeniceSoft.Abp.Auth.Repository** | `IRowPermissionRepository`, row-permission predicate builder |
| **BeniceSoft.Abp.Auth.EntityFrameworkCore** | `RowPermissionEfCoreRepository`, `AddRowPermissionRepositories<TDbContext>()`, field-permission SaveChanges interceptor |
| **BeniceSoft.Abp.Auth** | `BeniceSoftCurrentUser` (OpenIddict claims), permission middleware, `[FunctionPermission]`, field auth filter |
| **BeniceSoft.Abp.MultiTenancy** | Tenant from `IBeniceSoftCurrentUser.TenantId`; passthrough `ITenantStore` (pulled in by AspNetCore) |

### ASP.NET Core / HTTP / discovery

| Package | Notes |
|---------|--------|
| **BeniceSoft.Abp.AspNetCore** | `JsonFormatResponseFilter` → `ResponseResult<T>`, exception middleware, culture map |
| **BeniceSoft.Abp.Http.Client** | Proxy HTTP client (forwards Authorization), machine `client_credentials` token provider |
| **BeniceSoft.Abp.ServiceDiscovery** | `AddHttpServiceDiscovery` — register / health / metadata (HTTP or Redis) |
| **BeniceSoft.Abp.Swagger** | XML comments, enum Description filter, Bearer, persisted auth |
| **BeniceSoft.OAuth.DingTalk** | DingTalk OAuth (QR + silent app login) |

### Caching / lock / rate limit / Redis

| Package | Notes |
|---------|--------|
| **…Caching.Abstractions** | `[Cacheable]` (Key / Condition / Unless / ExpirationSeconds) |
| **…Caching** | AOP interceptor; prefix & default TTL from `BeniceSoft:Caching` |
| **…Caching.MessagePack** / **…SystemTextJson** | Value serializers |
| **…DistributedLock.Abstractions** | `[DistributedLock]`, `IDistributedLockProvider` |
| **…DistributedLock** | Redis Lua lock + auto-renew (not RedLock.net) |
| **…RateLimiting.Abstractions** | `[RateLimit]` — Ip / UserId / TenantId / Custom / Global |
| **…RateLimiting** | Redis token-bucket AOP (`RateLimiting` config) |
| **…Redis** | Command wrappers, Lua lock, Pub/Sub |

### Dynamic query

| Package | Notes |
|---------|--------|
| *(models in **BeniceSoft.Core**)* | `IDynamicQueryRequest`, condition groups, operators |
| **…DynamicQuery.EfCore** | LINQ expression from request |
| **…DynamicQuery.Sql** | SqlKata → parameterized SQL (SqlServer / PostgreSQL / MySQL / …) |

### Messaging & transactions

| Package | Notes |
|---------|--------|
| **…Extensions.RabbitMQ** | Work / Fanout / Direct / Topic publishers & consumers on ABP connection pool |
| **…EventBus.Dtm** | DTM outbox / inbox / UoW plumbing |
| **…EventBus.Dtm.Http** | Dtmcli HTTP, TCC / SAGA (`IGlobalTransaction`), `[DtmBranch]`, `UseDtmHttpMiddleware()` |
| **…EventBus.Dtm.EntityFrameworkCore** | EF outbox/inbox + barriers (MySQL / PG / SQL Server / SQLite) |

### Logging & audit

| Package | Notes |
|---------|--------|
| **…OperationLogging.Abstractions** | `[OperationLog]`, `OperationLogContext` |
| **…OperationLogging** | AOP interceptor |
| **…OperationLogging.EventBus** / **…Redis** | Dispatch via distributed event bus or Redis Pub/Sub |
| **…AuditTrail.Abstractions** | `EntityChangeRecord`, `IEntityChangeDispatcher`, options |
| **…AuditTrail.EventBus** | Publishes `EntityChangeEvent` (`[AuditTracked]` on domain; capture in EF DbContext) |

### Other

| Package | Notes |
|---------|--------|
| **…Extensions.Emailing** | MailKit SMTP `IEmailSender` from `Smtp` config |
| **BeniceSoft.Office.Excel** | NPOI import/export (`ExcelMapper`), `[ExcelColumn]`, template `ExcelReport` — no AbpModule |
| **BeniceSoft.Office.Pdf** | Parse text/fields/barcodes/PNG pages (`Pdf` / `IPdfParser`) — no AbpModule |

## Host sketch

```csharp
[DependsOn(
    typeof(BeniceSoftAbpAspNetCoreModule),
    typeof(BeniceSoftAbpSwaggerModule),
    typeof(BeniceSoftAbpAuthModule),
    typeof(BeniceSoftAbpDistributedLockModule),
    typeof(BeniceSoftAbpCachingMessagePackModule),
    typeof(BeniceSoftAbpRateLimitingModule),       // optional
    typeof(BeniceSoftAbpOperationLoggingEventBusModule),
    typeof(BeniceSoftAbpAuditTrailEventBusModule),
    typeof(AbpAutofacModule),                      // required for AOP
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

Declarative AOP (needs `virtual` + Autofac): `[Cacheable]`, `[DistributedLock]`, `[RateLimit]`, `[OperationLog]`.

EF provider: depend on **PostgreSql** or **SqlServer** module. Sharding: see [docs/分库分表使用指南.md](./docs/分库分表使用指南.md). Sample APIs under `samples/BeniceSoft.Abp.Sample.Host`.

```bash
dotnet build BeniceSoft.Abp.sln
cd samples/BeniceSoft.Abp.Sample.Host && dotnet run
```

## License

[MIT](./LICENSE)

