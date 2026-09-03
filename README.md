# BeniceSoft Framework

基于 [ABP Framework](https://abp.io/) 构建的企业级 .NET 开发框架，提供统一的基础设施层，涵盖 DDD 分层架构、数据权限、缓存、分布式锁、动态查询、操作日志、数据变更审计、Swagger 文档、服务发现等能力。

## 技术栈

- .NET 10
- ABP Framework 10.x（Volo.Abp）
- Entity Framework Core 10
- Redis（StackExchange.Redis）
- OpenIddict（OAuth/OIDC）

## 项目结构

```
BeniceSoft.Abp/
├── src/                          # 框架源码
├── samples/                      # 业务模板（Host API 联调 Bulk / QueryFuture / 分片等）
├── docs/                         # 架构与接入文档
├── common.props                  # 公共 MSBuild 属性
└── Directory.Packages.props      # NuGet 中央包管理
```

## 架构文档

| 文档 | 说明 |
|------|------|
| [docs/GRPC-SDK-ARCHITECTURE.md](./docs/GRPC-SDK-ARCHITECTURE.md) | gRPC Sdk 与编译期 proto 生成器架构设计（服务间调用演进方案） |
| [docs/分库分表使用指南.md](./docs/分库分表使用指南.md) | 业务服务接入分库/分表步骤（WarehouseCenter + B2C 订单举例，可复制代码） |

## 类库说明

### 🧱 核心层

| 类库 | 说明 |
|------|------|
| **BeniceSoft.Core** | 基础工具库（无 ABP 依赖）。包含：Singleton、高性能反射器、雪花 ID、分组算法、JSON 工具、**DeepClone / ShallowClone**、**FluentClient** 链式 HTTP 客户端（原独立包 `BeniceSoft.Http.FluentClient` 已并入）、丰富扩展方法 |
| **BeniceSoft.Abp.Core** | ABP 核心模块。提供：统一响应模型 `ResponseResult<T>`、当前用户接口 `IBeniceSoftCurrentUser`、自定义特性（`IgnoreJsonFormatAttribute`、`IgnoreBindAttribute`、`FillBasicDataValueAttribute`）、HTTP 常量定义、自定义异常类型 |

### 🏗️ DDD 分层

| 类库 | 说明 |
|------|------|
| **BeniceSoft.Abp.Ddd.Domain** | 领域层基础。提供：自定义审计实体基类（`BeniceSoftFullAuditedEntity<TKey>`、`BeniceSoftFullAuditedAggregateRoot<TKey>`，使用 `DateTimeOffset` + `long` 类型的审计字段）、`IQueryableWrapper` / `IQueryableWrapperFactory` 查询包装器接口、`ISqlExecuter` SQL 执行器接口 |
| **BeniceSoft.Abp.Ddd.Application.Contracts** | 应用层契约。依赖 `BeniceSoft.Abp.Core`，引入动态查询和分布式锁抽象 |
| **BeniceSoft.Abp.Ddd.Application** | 应用层基础。提供 `BeniceSoftApplicationService` 基类，内置 `IQueryableWrapperFactory` 支持 |

### 💾 数据访问

| 类库 | 说明 |
|------|------|
| **BeniceSoft.Abp.EntityFrameworkCore** | EF Core 基础设施。提供：`BeniceSoftAbpDbContext`、`EfCoreQueryableWrapper`、`DapperSqlExecuter`（报表/临时 SQL）、雪花 ID 值生成器、命名约定、无关系迁移差异器、**ForceSaveChange**（并发冲突重试）、**QueryFuture**（多查询批执行）、Bulk 抽象 |
| **BeniceSoft.Abp.EntityFrameworkCore.Sharding** | **完整分库分表引擎**。谓词自动路由、`AsRoute` / `UseMerge` / `AsSequence`、流式合并、读写分离与建表 Job；与 ABP 壳 DbContext / UoW / `IRepository` 对接。Sample 业务模板在 EF 模块内一并注册分片，启动 Host 调 `/api/sample/sharding-sample/*`；引擎测试见 `tests/...Sharding.Tests` |
| **BeniceSoft.Abp.EntityFrameworkCore.SqlServer** | SQL Server 提供程序扩展：**Bulk**（SqlBulkCopy + MERGE）、**Sequence**（`NEXT VALUE FOR`）、**Hint**（`WITH (NOLOCK)` 等表提示） |
| **BeniceSoft.Abp.EntityFrameworkCore.PostgreSql** | PostgreSQL 提供程序扩展：**Bulk**（COPY Binary + UPDATE/DELETE/ON CONFLICT）、**Sequence**（`NEXTVAL`）、**Hint**（`FOR UPDATE` / `FOR SHARE`） |

### 🔐 认证与权限

| 类库 | 说明 |
|------|------|
| **BeniceSoft.Abp.Auth.Core** | 权限核心抽象。定义：`IUserPermission`（行权限 + 字段权限）、`ICurrentUserPermissionAccessor`、`FieldAuthAttribute` 字段权限标签、行权限模型（`RowPermission`）、字段权限模型（`FieldPermission`） |
| **BeniceSoft.Abp.Auth.Repository** | 行权限仓储抽象。定义 `IRowPermissionRepository<TEntity, TKey>` 接口（继承 ABP `IRepository`，增加 `GetQueryableWithoutRowFilterAsync` 方法）、行权限谓词构建 `RepositoryExtensions.BuildRowPermissionPredicate` |
| **BeniceSoft.Abp.Auth.EntityFrameworkCore** | 行权限仓储 EF Core 实现。`RowPermissionEfCoreRepository` 自动应用行权限过滤、`AddRowPermissionRepositories<TDbContext>()` 批量注册带行权限的仓储 |
| **BeniceSoft.Abp.Auth** | 认证授权模块。提供：`BeniceSoftCurrentUser`（基于 OpenIddict Claims）、`CurrentUserPermissionAccessor`（AsyncLocal 存储）、字段权限过滤器 `FieldAuthFilterAttribute`、认证/授权/权限中间件 |

### 🌐 ASP.NET Core

| 类库 | 说明 |
|------|------|
| **BeniceSoft.Abp.AspNetCore** | ASP.NET Core 增强模块。提供：统一响应格式化过滤器 `JsonFormatResponseFilter`（自动包装为 `ResponseResult<T>`）、全局异常处理中间件 `ExceptionHandlingMiddleware`（区分远程服务调用和普通请求）、本地化增强（Cookie 文化映射） |

### 📦 缓存

| 类库  | 说明 |
|------|------|
| **BeniceSoft.Abp.Extensions.Caching.Abstractions** | 缓存抽象。定义：`CacheableAttribute`（声明式缓存，支持 Key 表达式、条件判断、Unless 表达式、过期时间）、`ICacheKeyGenerator`、`ICacheValueSerializer` |
| **BeniceSoft.Abp.Extensions.Caching** | 缓存核心实现。`CacheableInterceptor` AOP 拦截器（自动缓存方法返回值）、`SimpleCacheKeyGenerator` 缓存键生成、可配置的缓存前缀和默认过期时间 |
| **BeniceSoft.Abp.Extensions.Caching.MessagePack** | MessagePack 序列化器实现（高性能、体积小） |
| **BeniceSoft.Abp.Extensions.Caching.SystemTextJson** | System.Text.Json 序列化器实现 |

### 🔒 分布式锁

| 类库  | 说明 |
|------|------|
| **BeniceSoft.Abp.Extensions.DistributedLock.Abstractions** | 分布式锁抽象。定义：`IDistributedLockProvider`（获取/释放/续期锁）、`DistributedLockAttribute`（声明式分布式锁，支持资源 ID 模板、过期时间、等待时间、自动续期） |
| **BeniceSoft.Abp.Extensions.DistributedLock** | 基于 RedLock.net 的分布式锁实现。`RedLockDistributedLockProvider`（支持自动续期 Watchdog、指标收集、健康检查）、`DistributedLockInterceptor` AOP 拦截器 |

### 🔍 动态查询

| 类库 | 说明 |
|------|------|
| **BeniceSoft.Abp.Extensions.DynamicQuery.Abstractions** | 动态查询抽象。定义：`IDynamicQueryRequest`、`DynamicQueryCondition`（字段名、字段类型、操作符、值）、支持的操作符（Equal / NotEqual / Contains / Between / In 等） |
| **BeniceSoft.Abp.Extensions.DynamicQuery.EfCore** | EF Core 动态查询实现。将 `IDynamicQueryRequest` 转换为 LINQ 表达式树，支持嵌套属性、多种数据类型 |
| **BeniceSoft.Abp.Extensions.DynamicQuery.Sql** | 原生 SQL 动态查询实现。基于 SqlKata，将 `IDynamicQueryRequest` 转换为参数化 SQL，支持 SqlServer / PostgreSql / MySql |

### 🗄️ Redis

| 类库 | 说明 |
|------|------|
| **BeniceSoft.Abp.Extensions.Redis** | Redis 客户端封装。基于 StackExchange.Redis，提供：完整的 Redis 命令封装（String / List / Hash / Set / SortedSet / Key）、连接管理（自动重连、事件日志）、分布式锁（Lua 脚本保证原子性）、Pub/Sub 消息队列 |

### 📝 操作日志

| 类库 | 说明 |
|------|------|
| **BeniceSoft.Abp.OperationLogging.Abstractions** | 操作日志抽象。定义：`OperationLogAttribute`（声明式操作日志）、`OperationLogInfo`（日志信息模型）、`OperationLogContext`（运行时上下文，支持动态设置业务 ID、编码、备注、扩展数据） |
| **BeniceSoft.Abp.OperationLogging** | 操作日志核心。`OperationLogInterceptor` AOP 拦截器，自动记录操作人、操作时间、业务信息 |
| **BeniceSoft.Abp.OperationLogging.EventBus** | 基于 ABP 分布式事件总线的日志分发实现 |
| **BeniceSoft.Abp.OperationLogging.Redis** | 基于 Redis Pub/Sub 的日志分发实现 |

### 📊 数据变更审计追踪

| 类库 | 说明 |
|------|------|
| **BeniceSoft.Abp.Extensions.AuditTrail.Abstractions** | 变更审计抽象。定义：`EntityChangeRecord`（实体变更记录，含实体类型、Id、变更类型、操作人、时间）、`PropertyChangeInfo`（属性变更明细）、`IEntityChangeDispatcher`（变更分发接口） |
| **BeniceSoft.Abp.Extensions.AuditTrail** | 变更审计核心。`NullEntityChangeDispatcher`（默认空实现）、`BeniceSoftAbpAuditTrailOptions`（配置项：启用开关、排除实体类型列表） |
| **BeniceSoft.Abp.Extensions.AuditTrail.EventBus** | 基于 ABP 分布式事件总线的变更分发实现。`EventBusEntityChangeDispatcher` 将变更记录通过 `IDistributedEventBus` 发布为 `EntityChangeEvent` |

> 字段级标记属性 `[AuditTracked]` 定义在 `BeniceSoft.Abp.Ddd.Domain` 中，采集逻辑 `AuditTrailChangeTracker` 集成在 `BeniceSoft.Abp.EntityFrameworkCore` 的 `BeniceSoftAbpDbContext` 中，SaveChanges 时自动采集并分发。

### 📖 Swagger

| 类库 | 说明 |
|------|------|
| **BeniceSoft.Abp.Swagger** | Swagger 文档增强模块。提供：`BeniceSoftSwaggerOptions`（丰富的配置项）、自动加载 XML 注释、枚举描述过滤器（`EnumDescriptionSchemaFilter`，自动展示 Description/Display 特性）、Bearer 认证配置、持久化授权 |

### 🌍 远程服务

| 类库 | 说明 |
|------|------|
| **BeniceSoft.Abp.Http.Client** | HTTP 客户端增强。`BeniceSoftProxyHttpClientFactory` 自动传递 Authorization Header、标识远程服务调用、跳过响应格式化 |
| **BeniceSoft.Abp.ServiceDiscovery** | 服务发现模块。支持 HTTP / Redis 两种注册方式，提供：自动注册/注销、健康检查、服务元数据（版本、环境、权重）、启动重试策略 |

### 🔑 第三方认证

| 类库 | 说明 |
|------|------|
| **BeniceSoft.OAuth.DingTalk** | 钉钉 OAuth 认证处理器。支持扫码登录和微应用免登，自动获取用户详细信息 |

## 快速开始

### 1. Host 模块配置

一个典型的 Host 模块引用和中间件配置：

```csharp
[DependsOn(
    typeof(BeniceSoftAbpAspNetCoreModule),       // ASP.NET Core 增强
    typeof(BeniceSoftAbpSwaggerModule),           // Swagger 文档
    typeof(BeniceSoftAbpAuthModule),              // 认证授权
    typeof(BeniceSoftAbpDistributedLockModule),   // 分布式锁
    typeof(BeniceSoftAbpCachingMessagePackModule),// 缓存（MessagePack 序列化）
    typeof(BeniceSoftAbpOperationLoggingEventBusModule), // 操作日志（EventBus 分发）
    typeof(BeniceSoftAbpAuditTrailEventBusModule),       // 数据变更审计（EventBus 分发）
    typeof(AbpAutofacModule),                     // Autofac 容器（AOP 拦截器需要）
    typeof(YourApplicationModule),
    typeof(YourEntityFrameworkCoreModule)
)]
public class YourHostModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // Swagger 配置
        PreConfigure<BeniceSoftSwaggerOptions>(options =>
        {
            options.Title = "My API";
            options.Version = "v1";
            options.Description = "My API 文档";
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        // 自动注册 Application 层为 API Controller
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers
                .Create(typeof(YourApplicationModule).Assembly);
        });

        // 注册自定义授权策略
        context.Services.AddBeniceSoftAuthorization();

        // 添加 HTTP 服务发现（可选）
        context.Services.AddHttpServiceDiscovery(options =>
        {
            configuration.GetSection("ServiceDiscovery").Bind(options);
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        app.UseCorrelationId();
        app.UseRouting();
        app.UseCors(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

        // 全局异常处理（自动区分远程服务调用和普通请求）
        app.UseBeniceSoftExceptionHandlingMiddleware();

        app.UseAbpRequestLocalization();

        // 认证 → 授权 → 用户权限（三个中间件按顺序使用）
        app.UseBeniceSoftAuthentication();
        app.UseBeniceSoftAuthorization();
        app.UseBeniceSoftUserPermission();

        app.UseAuditing();

        // Swagger
        app.UseBeniceSoftSwagger();

        app.UseConfiguredEndpoints();
    }
}
```

`appsettings.json` 配置示例：

```json
{
  "Auth": {
    "Authority": "http://localhost:6002",
    "PermissionCenterUrl": "http://localhost:5003",
    "Audience": "your-audience",
    "SecurityKey": "your-security-key"
  },
  "ConnectionStrings": {
    "Default": "server=localhost;port=5432;username=postgres;password=postgres;database=mydb;"
  },
  "Redis": {
    "Configuration": "localhost:6379,defaultDatabase=0"
  },
  "DistributedLock": {
    "ConnectionString": "localhost:6379,defaultDatabase=1"
  },
  "ServiceDiscovery": {
    "ServiceName": "my-service",
    "GatewayBaseUrl": "http://localhost:5056",
    "Metadata": {
      "version": "1.0.0",
      "environment": "development"
    }
  },
  "RemoteServices": {
    "Wecharmer.OtherService": {
      "BaseUrl": "http://localhost:5001/"
    }
  }
}
```

### 2. 领域层 — 实体定义

使用自定义审计基类（`DateTimeOffset` + `long` 类型审计字段）：

```csharp
// 聚合根
public class Order : BeniceSoftFullAuditedAggregateRoot<long>, IHaveOwnerId
{
    public string OrderNo { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public long OwnerId { get; set; } // 实现 IHaveOwnerId，创建时自动填充当前用户 ID
}

// 普通实体
public class OrderItem : BeniceSoftFullAuditedEntity<long>
{
    public long OrderId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

// 也提供不含软删除的审计基类
public class Tag : BeniceSoftAuditedEntity<long>
{
    public string Name { get; set; } = string.Empty;
}
```

### 3. 数据访问层 — EF Core 配置

```csharp
// DbContext
public class MyDbContext : BeniceSoftAbpDbContext<MyDbContext>
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    public MyDbContext(DbContextOptions<MyDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // 配置实体映射...
    }
}

// EF Core 模块 — 按库引用 SqlServer 或 PostgreSql 提供程序模块
[DependsOn(typeof(BeniceSoftAbpEntityFrameworkCorePostgreSqlModule))]
public class MyEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<MyDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
        });

        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure<MyDbContext>(ctx =>
            {
                ctx.UseNpgsql();
                ctx.DbContextOptions.ForRowState(); // 启用 PG FOR UPDATE / FOR SHARE
            });
        });

        // 注册带行权限的仓储（可选）
        context.Services.AddRowPermissionRepositories<MyDbContext>();
    }
}
```

### 4. 应用层 — 服务编写

```csharp
// Application 模块
[DependsOn(
    typeof(BeniceSoftAbpDddApplicationModule),
    typeof(BeniceSoftAbpOperationLoggingModule),
    typeof(YourApplicationContractsModule)
)]
public class YourApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<BeniceSoftOperationLogOptions>(options =>
        {
            options.ServiceName = "OrderService"; // 操作日志中的服务名
        });
    }
}

// 应用服务基类内置 IQueryableWrapperFactory 支持
public class OrderAppService : BeniceSoftApplicationService
{
    private readonly IRepository<Order, long> _orderRepository;

    public OrderAppService(IRepository<Order, long> orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<PagedResultDto<OrderDto>> GetListAsync(OrderQueryReq input)
    {
        var queryable = await _orderRepository.GetQueryableAsync();

        // 使用 IQueryableWrapper 链式查询
        var wrapper = QueryableWrapperFactory.CreateWrapper(queryable)
            .AsNoTracking()
            .SearchByKey(input.SearchKey, x => x.OrderNo) // 关键字搜索
            .DynamicQueryBy(input)                         // 动态查询
            .WhereIf(input.Status.HasValue, x => x.Status == input.Status)
            .OrderByDescending(x => x.CreationTime);

        var count = await wrapper.CountAsync();
        var items = await wrapper.PageBy(input.SkipCount, input.MaxResultCount).ToListAsync();

        return new PagedResultDto<OrderDto>(count, ObjectMapper.Map<List<Order>, List<OrderDto>>(items));
    }
}
```

### 5. 声明式缓存

方法上标注 `[Cacheable]`，返回值自动缓存（需要 `virtual` 方法 + Autofac 容器）：

```csharp
// 基础用法 — 默认缓存键（方法签名 + 参数类型）
[Cacheable(ExpirationSeconds = 60)]
public virtual async Task<ProductDto> GetProductAsync(int productId)
{
    return await _repository.GetAsync(productId);
}

// 自定义缓存键（支持参数内插）
[Cacheable(Key = "user:{id}", ExpirationSeconds = 300)]
public virtual async Task<UserDto> GetByIdAsync(long id)
{
    return await _repository.GetAsync(id);
}

// 条件缓存 + Unless 表达式
[Cacheable(
    Key = "order:{orderId}",
    Condition = "orderId > 0",           // 仅 orderId > 0 时缓存
    Unless = "result == null",           // 返回 null 时不缓存
    ExpirationSeconds = 600)]
public virtual async Task<OrderDto?> GetOrderAsync(long orderId)
{
    return await _repository.FindAsync(orderId);
}
```

`appsettings.json` 缓存配置（可选）：

```json
{
  "BeniceSoft": {
    "Caching": {
      "CacheKeyPrefix": "MyApp:Cached:",
      "DefaultExpirationSeconds": 1800
    }
  }
}
```

### 6. 声明式分布式锁

方法上标注 `[DistributedLock]`，执行期间自动持有 Redis 分布式锁：

```csharp
// 基础用法 — 默认使用方法全名作为资源 ID
[DistributedLock]
public virtual async Task ProcessAsync()
{
    // 方法执行期间自动持有锁
}

// 自定义资源 ID（支持参数内插）
[DistributedLock(ResourceId = "order:create:{orderId}")]
public virtual async Task CreateOrderAsync(long orderId)
{
    // 同一 orderId 不会并发执行
}

// 长时任务 — 启用自动续期
[DistributedLock(
    ResourceId = "report:generate:{reportId}",
    ExpiresMilliseconds = 30000,   // 锁过期时间 30s
    WaitMilliseconds = 10000,      // 等待获取锁最长 10s
    AutoRenew = true)]             // 自动续期（Watchdog）
public virtual async Task GenerateReportAsync(long reportId)
{
    // 长时间运行，锁会自动续期直到方法完成
}
```

### 7. 操作日志

方法上标注 `[OperationLog]`，自动记录操作人、操作时间、业务信息：

```csharp
[OperationLog(OperationType = "Create", BizModule = "Order")]
public virtual async Task CreateAsync(CreateOrderDto dto, OperationLogContext? logContext = null)
{
    var order = ObjectMapper.Map<CreateOrderDto, Order>(dto);
    await _repository.InsertAsync(order);

    // logContext 由拦截器自动注入（方法最后一个参数为 OperationLogContext 类型时）
    logContext?.SetValue(
        bizId: order.Id.ToString(),
        bizCode: order.OrderNo,
        remark: "创建订单",
        extraData: new Dictionary<string, object> { ["amount"] = order.Amount }
    );
}

[OperationLog(OperationType = "Delete", BizModule = "Order", BizId = "静态业务ID")]
public virtual async Task DeleteAsync(long id)
{
    await _repository.DeleteAsync(id);
}
```

### 8. 动态查询

前端传入查询条件，后端自动构建 LINQ 表达式或 SQL：

```csharp
// 定义查询请求 DTO（实现 IDynamicQueryRequest）
public class OrderQueryReq : PagedResultRequestDto, IDynamicQueryRequest
{
    public List<DynamicQueryConditionGroup>? ConditionGroups { get; set; }
    public string? SearchKey { get; set; }
}

// EF Core 用法 — 自动转换为 LINQ 表达式树
var queryable = await _repository.GetQueryableAsync();
var result = queryable.DynamicQueryBy(request); // request 实现 IDynamicQueryRequest

// 原生 SQL 用法 — 基于 SqlKata 转换为参数化 SQL
var sqlResult = "SELECT * FROM Orders".DynamicQueryBy(request, SqlCompilerType.PostgreSql);
// sqlResult.Sql → 参数化 SQL
// sqlResult.Bindings → 参数值
```

前端传入的 JSON 格式：

```json
{
  "conditionGroups": [{
    "relation": "and",
    "conditions": [
      { "fieldName": "OrderNo", "fieldType": "string", "operator": "contains", "value": ["ORD"] },
      { "relation": "and", "fieldName": "Amount", "fieldType": "double", "operator": "greater_than", "value": ["100"] },
      { "relation": "and", "fieldName": "CreationTime", "fieldType": "datetime", "operator": "between", "value": ["2024-01-01", "2024-12-31"] }
    ]
  }]
}
```

支持的操作符：`equal` / `not_equal` / `greater_than` / `greater_than_or_equal` / `less_than` / `less_than_or_equal` / `contains` / `not_contains` / `starts_with` / `ends_with` / `between` / `in` / `not_in`

支持的字段类型：`string` / `integer` / `long` / `double` / `boolean` / `date` / `datetime` / `guid`

### 9. Redis 客户端

自动从 `Redis:Configuration` 读取连接字符串，注入 `RedisClient` 使用：

```csharp
public class MyService
{
    private readonly RedisClient _redis;

    public MyService(RedisClient redis)
    {
        _redis = redis;
    }

    public async Task ExampleAsync()
    {
        // String 操作
        await _redis.SetAsync("key", new { Name = "test" }, TimeSpan.FromMinutes(5));
        var value = await _redis.GetAsync<MyDto>("key");

        // Hash 操作
        await _redis.HSetAsync("hash:key", "field", "value");

        // 分布式锁（Lua 脚本保证原子性）
        using var lockObj = await _redis.LockAsync("my-resource", expirySeconds: 30);
        if (lockObj?.IsAcquired == true)
        {
            // 持有锁期间执行操作
        }
    }
}
```

### 10. 远程服务调用

#### HTTP 客户端代理

```csharp
// SDK 模块（供其他服务引用）
[DependsOn(typeof(BeniceSoftAbpHttpClientModule))]
public class OrderSdkModule : AbpModule
{
    public const string RemoteServiceName = "Wecharmer.Order";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 自动扫描 Application.Contracts 中的接口，生成 HTTP 客户端代理
        context.Services.AddHttpClientProxies(
            typeof(OrderApplicationContractsModule).Assembly,
            RemoteServiceName
        );
    }
}
```

`appsettings.json`：

```json
{
  "RemoteServices": {
    "Wecharmer.Order": {
      "BaseUrl": "http://localhost:5001/"
    }
  }
}
```

### 11. EF Core 增强（Bulk / QueryFuture / Sequence / Hint / ForceSave）

依赖对应提供程序模块（二选一）：

```csharp
[DependsOn(typeof(BeniceSoftAbpEntityFrameworkCorePostgreSqlModule))]
// 或
[DependsOn(typeof(BeniceSoftAbpEntityFrameworkCoreSqlServerModule))]
public class YourEntityFrameworkCoreModule : AbpModule { }
```

PostgreSQL 启用行锁 Hint 时，在 DbContext 选项上调用 `ForRowState()`；SQL Server 表提示调用 `WithTableHint()`：

```csharp
Configure<AbpDbContextOptions>(options =>
{
    options.Configure<YourDbContext>(ctx =>
    {
        ctx.UseNpgsql();
        ctx.DbContextOptions.ForRowState();   // PG
        // ctx.DbContextOptions.WithTableHint(); // SQL Server
    });
});
```

#### Bulk 批量写

绕过 ChangeTracker / SaveChanges（不走审计管道）。Insert 走协议级装载（PG: COPY，SQL Server: SqlBulkCopy）；Update/Delete/Merge 先灌临时表再集合 SQL。

```csharp
// 一次性插入
await db.BulkInsertAsync(items);

// 按主键批量更新 / 删除 / Upsert（匹配列默认可为 PK；PG Merge 匹配列需有唯一约束）
await db.BulkUpdateAsync(items);
await db.BulkDeleteAsync(items);
await db.BulkMergeAsync(items, matchBuilder: m => m.MatchTargetOn(x => x.Code));

// 剔除不需要写入的列
await db.BulkInsertAsync(items, atom => atom.RemoveColumn(x => x.Version).WithCommandTimeout(120));

// 多步共用同一事务（需显式 Commit）
await using var op = db.BulkOperation();
await op.BulkInsertAsync(batch1);
await op.BulkUpdateAsync(batch2);
await op.CommitAsync();
```

> Sample Host API 联调 Bulk；自动化见 `tests/BeniceSoft.Abp.Sample.Tests/PgBulkIntegrationTests.cs`、`PgBulkPerfTests.cs`。

#### 逻辑多租户 vs 分片分库

| 能力 | 做什么 | Sample |
|------|--------|--------|
| ABP 逻辑多租户 | `ICurrentTenant` / `__tenant` / 过滤器 | Host 可启用；仅逻辑身份 |
| ABP 连接串拆库 | `MultiTenantConnectionStringResolver` + `Tenants[].ConnectionStrings` | **不要**与分片 DbContext 叠用 |
| 分片按租户分物理库 | `DataSourceRoute(TenantId)` 取模/映射 → ds0..dsN | 见分库分表指南 |

启用分片后，壳连接由 **VirtualDataSource** 决定。API `/sample/tenant-isolation/*` 只探测逻辑租户 + 实际 VDS 默认库名。

#### 分库分表（BeniceSoft.Abp.EntityFrameworkCore.Sharding）

完整引擎（非简易换表名）。Sample 按「订单服务只分订单表」接好，其余表普通。

**业务服务逐步接入（以 WarehouseCenter + B2C 订单举例，含可复制代码）：** [`docs/分库分表使用指南.md`](docs/分库分表使用指南.md)

##### 复制清单（Sample 内路径）

| 步骤 | 文件 | 作用 |
|------|------|------|
| 1. 实体 | `samples/.../Domain/ShardingDemoEntities.cs` | `SalesOrder`（按月分表）+ `Product`（普通表） |
| 2. 路由 | `samples/.../EntityFrameworkCore/Sharding/SampleShardingRoutes.cs` | 仅 `SalesOrderMonthRoute` |
| 3. 注册 | `SampleEntityFrameworkCoreModule` 内直接 `AddSharding` + `UseShardingAfter` | 单库 + 只注册订单路由 |
| 4. 壳 DbContext | `SampleDbContext`：`BeniceSoftShardingAbpDbContext` + `IShardingTableDbContext` | |
| 5. Migration | `Migrations/*` → `dotnet ef database update` 或导出 SQL 执行 | **先**生成实体对应物理表 |
| 6. Host 启动 | `Migrate()` 之后 `UseCompensate()` | **再**为分片实体创建分表物理表（如 `sales_orders_yyyyMM`） |
| 7. 联调 | `/api/sample/sharding-sample/*` | |
| 8. 引擎测试 | `tests/BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests` | 引擎自建测试库；业务服务仍按上表规范 |

##### 建表规范（固定顺序）

```text
1. 写实体 + ToTable（逻辑表名）
2. 生成并执行 Migration（update-database 或 SQL 脚本）→ 库中已有物理表
3. 若该实体需要分表：注册 TableRoute，启动应用时 Compensate → 创建分表物理表（后缀）
```

禁止：用 `IF NOT EXISTS` / Compensate 代替 Migration 建实体表；禁止先 Compensate 再 Migrate。

##### 能力一览

| 能力 | 说明 |
|------|------|
| 自动路由 | `Where` 含分片键等值时定位物理表；无谓词时常扇出再合并 |
| 显式路由 | `AsRoute(ctx => …)`：`MustTable` / `HintTable`（路由需 `EnabledHint => true`） |
| 混用 | 未 `AddTableRoute` 的实体走 Migration 落下的单表 |
| ABP | 只持壳 DbContext / `IRepository` / UoW；物理上下文由 Executor 创建 |
| Bulk + 分片 | Module 同时 DependsOn PostgreSql + Sharding；Options：`UseNpgsql` → `ForRowState` → `UseShardingAfter`（分片必须最后） |

##### 注册（与 Sample 一致，Module 内联，无额外 Registrar）

```csharp
services.AddSharding<YourDbContext>()
    .UseOptions((_, options) =>
    {
        options.WithDefaultDataSource("ds0", connectionString);
        options.WithShardingQuery((connStr, b) =>
        {
            b.UseNpgsql(connStr);
            b.ForRowState();
        });
        options.WithShardingTransaction((conn, b) =>
        {
            b.UseNpgsql(conn);
            b.ForRowState();
        });
        options.IgnoreCreateTableError = false;
    })
    .UseRouteOptions((_, routes) =>
    {
        routes.AddTableRoute<SalesOrderMonthRoute>();
    });

Configure<AbpDbContextOptions>(options =>
{
    options.Configure<YourDbContext>(ctx =>
    {
        ctx.UseNpgsql();
        ctx.DbContextOptions.UseShardingAfter<YourDbContext>(ctx.ServiceProvider, b => b.ForRowState());
    });
});

// Host OnApplicationInitialization — 顺序固定：
db.Database.Migrate();              // ① 实体物理表（含分片实体的逻辑表结构）
serviceProvider.UseCompensate();    // ② 仅分表物理表（如 sales_orders_yyyyMM）
```

二次 `Configure` 覆盖连接串时，必须再 `UseNpgsql(cs)` + `UseShardingAfter`，禁止只写 `UseNpgsql`。

##### 业务约定

1. **建表：先 Migration，再 Compensate（仅分表实体）。**
2. **查询尽量带分片键等值**（如 `OrderTime ==`）；`!=` 等会扇出，靠约定避免。
3. **只给需要分片的实体加 TableRoute / DataSourceRoute**；其它实体不要注册。
4. **不要改已落库行的分片键字段**。
5. 若启用多物理库，**同一次 UoW 不要跨库提交**。
6. **分片 DbContext 不要叠 ABP 按租户连接串拆库**；租户物理隔离用 `DataSourceRoute(TenantId)`。

##### 联调 API

```text
POST /api/sample/sharding-sample/order-month-auto-route
POST /api/sample/sharding-sample/order-as-route-must
POST /api/sample/sharding-sample/order-fan-out-merge
POST /api/sample/sharding-sample/order-with-normal-product
```

更细的边界用例见 `tests/BeniceSoft.Abp.EntityFrameworkCore.Sharding.Tests`。

```text
Host 库 wecharmer_sample
  ├─ sales_orders_yyMM     ← 仅订单按月分表
  ├─ products / bulk_demo_items / am_*  ← 普通表
  ├─ tenant-a → wecharmer_sample_tenant_a
  └─ tenant-b → wecharmer_sample_tenant_b
```

#### QueryFuture（多查询一次往返）

同一 `DbContext` 上挂起多个互不依赖的 LINQ，首次取值时合并为一条多结果集命令。适合详情页/看板里多个都还不慢的读；**不适合**把很慢的 Count 和分页列表强绑在一个接口里（慢查询仍会拖垮整体 RT）。

```csharp
var listFuture = db.Orders.AsNoTracking().Where(x => x.Status == status).OrderBy(x => x.Id).Take(20).Future();
var sumFuture = db.Orders.AsNoTracking().Where(x => x.Status == status).Select(x => (decimal?)x.Amount).FutureValue();

var list = await listFuture.ToListAsync();   // 触发整批
var sum = await sumFuture.ValueAsync();      // 已物化，不再往返

// 关闭批处理（调试或兼容）
QueryFutureManager.AllowQueryBatch = false;
```

#### Sequence

```csharp
// 取 1 个 / 一批序列值（需库中已有 sequence）
var next = await db.Database.GetSequenceAsync<long>("order_seq");
var batch = await db.Database.GetSequenceAsync<long>("order_seq", count: 100);
```

#### Hint（行/表锁提示）

```csharp
// PostgreSQL
var locked = await db.Orders.Where(x => x.Id == id).ForUpdate().ToListAsync();
var shared = await db.Orders.Where(...).ForShare().ToListAsync();

// SQL Server
var dirty = await db.Orders.Where(...).WithNoLock().ToListAsync();
```

#### ForceSaveChange（乐观并发重试）

`SaveChanges` 遇到并发冲突时，同步并发令牌后按策略重试（会解开 ABP 包装的内部 `DbUpdateConcurrencyException`）：

```csharp
var affected = await db.ForceSaveChangeAsync(retryCount: 3);
```

### 12. Dapper SQL 执行器（报表 / 临时 SQL）

面向**报表、复杂聚合、非实体投影**等场景，不是业务服务的默认写路径。与 EF 共用连接与 UoW 事务：

```csharp
// 1. 定义 SqlExecutionContext 接口
public interface IOrderSqlExecutionContext : ISqlExecutionContext { }

// 2. 实现 SqlExecutionContext
public class OrderSqlExecutionContext : IOrderSqlExecutionContext
{
    private readonly IDbContextProvider<MyDbContext> _contextProvider;

    public OrderSqlExecutionContext(IDbContextProvider<MyDbContext> contextProvider)
    {
        _contextProvider = contextProvider;
    }

    public async Task<IDbTransaction?> GetCurrentDbTransactionAsync()
    {
        var dbContext = await _contextProvider.GetDbContextAsync();
        return dbContext.Database.CurrentTransaction?.GetDbTransaction();
    }

    public async Task<IDbConnection> GetDbConnectionAsync()
    {
        var dbContext = await _contextProvider.GetDbContextAsync();
        return dbContext.Database.GetDbConnection();
    }
}

// 3. 实现 SqlExecuter
[ExposeServices(typeof(ISqlExecuter<IOrderSqlExecutionContext>))]
public class OrderSqlExecuter : DapperSqlExecuter<IOrderSqlExecutionContext>, ITransientDependency
{
    public OrderSqlExecuter(ILogger<DapperSqlExecuter<IOrderSqlExecutionContext>> logger,
        IDbContextProvider<MyDbContext> contextProvider) : base(logger)
    {
        SqlExecutionContext = new OrderSqlExecutionContext(contextProvider);
    }

    public override IOrderSqlExecutionContext SqlExecutionContext { get; }
}

// 4. 使用
public class ReportService
{
    private readonly ISqlExecuter<IOrderSqlExecutionContext> _sqlExecuter;

    public async Task<List<OrderSummary>> GetSummaryAsync()
    {
        return await _sqlExecuter.QueryAsync<OrderSummary>(
            "SELECT status, COUNT(*) as Count, SUM(amount) as Total FROM orders GROUP BY status");
    }
}
```

### 13. 数据权限

通过 `AddRowPermissionRepositories<TDbContext>()` 注册的仓储会自动应用行权限过滤：

```csharp
// 使用带行权限的仓储接口
public class OrderAppService : BeniceSoftApplicationService
{
    private readonly IRowPermissionRepository<Order, long> _orderRepository;

    public async Task<List<OrderDto>> GetListAsync()
    {
        // 自动应用行权限过滤
        var queryable = await _orderRepository.GetQueryableAsync();

        // 如果需要跳过行权限过滤
        var allQueryable = await _orderRepository.GetQueryableWithoutRowFilterAsync();

        return ObjectMapper.Map<List<Order>, List<OrderDto>>(await queryable.ToListAsync());
    }
}

// 字段权限 — 在 DTO 属性上标注
public class OrderDto
{
    public string OrderNo { get; set; }

    [FieldAuth("Order.Amount")] // 无权限时该字段不返回
    public decimal Amount { get; set; }

    [FieldAuth("Order.CustomerName")]
    public string CustomerName { get; set; }
}

// 在 Controller 上使用字段权限过滤器
[FieldAuthFilter]
public class OrderController : AbpController { }
```

### 14. 服务发现

业务服务自动注册到网关，支持健康检查和心跳：

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    var configuration = context.Services.GetConfiguration();

    // 添加 HTTP 服务发现
    context.Services.AddHttpServiceDiscovery(options =>
    {
        configuration.GetSection("ServiceDiscovery").Bind(options);
    });
}
```

`appsettings.json`：

```json
{
  "ServiceDiscovery": {
    "ServiceName": "order-service",
    "GatewayBaseUrl": "http://gateway:5000",
    "Port": 5001,
    "HealthCheckEndpoint": "/health",
    "HeartbeatInterval": "00:00:10",
    "EnableAutoRegistration": true,
    "Metadata": {
      "version": "1.0.0",
      "environment": "production"
    }
  }
}
```

### 15. 钉钉 OAuth 登录

```csharp
services.AddAuthentication()
    .AddDingTalk(options =>
    {
        options.ClientId = configuration["OAuth:DingTalk:ClientId"]!;
        options.ClientSecret = configuration["OAuth:DingTalk:ClientSecret"]!;
        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
    });
```

### 16. 数据变更审计追踪

在实体属性上标注 `[AuditTracked]`，SaveChanges 时自动采集变更并通过分布式事件分发：

```csharp
// 1. 实体定义 — 标记需要追踪的字段
public class Order : BeniceSoftFullAuditedAggregateRoot<long>
{
    [AuditTracked(DisplayName = "订单状态")]
    public OrderStatus Status { get; set; }

    [AuditTracked(DisplayName = "收货地址")]
    public string Address { get; set; }

    [AuditTracked(DisplayName = "金额")]
    public decimal Amount { get; set; }

    // 未标记的字段不会被追踪
    public string InternalRemark { get; set; }
}

// 2. Host 模块引入 EventBus 分发
[DependsOn(
    typeof(BeniceSoftAbpAuditTrailEventBusModule)  // 分布式事件分发
)]
public class YourHostModule : AbpModule { }

// 3. 消费端 — 订阅 EntityChangeEvent 处理变更记录
public class EntityChangeEventHandler : IDistributedEventHandler<EntityChangeEvent>, ITransientDependency
{
    public async Task HandleEventAsync(EntityChangeEvent eventData)
    {
        // eventData.EntityType  → "Order"
        // eventData.ChangeType  → "Modified"
        // eventData.Changes     → [{ PropertyName: "Status", DisplayName: "订单状态", OriginalValue: "Pending", NewValue: "Shipped" }]
        // 写入审计日志表、发送通知等...
    }
}
```

> 未引入 `BeniceSoftAbpAuditTrailEventBusModule` 时，不会注册任何 `IEntityChangeDispatcher`，DbContext 自动跳过采集和分发，零开销。

### 17. BeniceSoft.Core 工具库

```csharp
// 雪花 ID 生成器
var idGen = new SnowIdGenerator(machineId: 1, sequenceBits: 10);
long id = idGen.NewSequenceId();

// 带日期前缀的雪花 ID（如：240210xxxx）
var dateIdGen = new SnowDateIdGenerator(machineId: 1, sequenceBits: 10, digit: 6, dateFormat: "yyMMdd");
long dateId = dateIdGen.NewSequenceId();

// 带前缀的 ID
string orderId = idGen.NewId("ORD", digits: 10); // "ORD0001234567"

// 高性能反射器
var reflector = typeof(Order).GetMethod("GetAmount")!.GetReflector();
var result = reflector.Invoke(orderInstance);

// Singleton 模式
Singleton<MyConfig>.Instance = new MyConfig();
var config = Singleton<MyConfig>.Instance;

// 深拷贝 / 浅拷贝（循环引用安全）
var copy = DeepCloner.DeepClone(order);           // 完整对象图
var shallow = DeepCloner.ShallowClone(order);     // 仅顶层，引用共享
DeepCloner.DeepClone(source, target);             // 深拷贝填充已有实例

// FluentClient 链式 HTTP（命名空间仍为 BeniceSoft.Http.FluentClient）
services.AddFluentClient("https://api.example.com");
await client.Get("users").WithArgument("page", 1).As<List<User>>();
```

## 许可证

MIT