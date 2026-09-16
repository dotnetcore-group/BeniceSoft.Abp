# Core

包：`BeniceSoft.Core`。无 ABP 依赖。命名空间主要是 `BeniceSoft.Core`；HTTP 客户端在 `BeniceSoft.Http.FluentClient`。

## 响应与分页

```csharp
var ok = data.ToSucceed();                 // ResponseResult<T>，Code = 200
var fail = new ResponseResult(400, "无效参数");
var page = new PagedList<OrderDto>(total, items);
```

`ResponseResult.IsSuccess` 为 `Code == 200`。`TraceId` 为空时不写出。`PagedList<T>` 的 `Data` 是 `PagedResult<T>`：`Items`、`TotalCount`。

分页请求继承 `PagedRequestBase`：`PageNumber` 从 1 开始，默认 1；`PageSize` 默认 10；另有 `SearchKey`。

`QueryableWrapper.ToPagedListAsync(pageNumber, pageSize)` 的页码从 1 开始。集合扩展 `Paged(pageIndex, pageSize)` 的 `pageIndex` 从 0 开始，偏移为 `pageIndex * pageSize`。

## 雪花 Id

进程启动时写入单例，早于 EF 模型创建：

```csharp
Singleton<SnowDateIdGenerator>.Instance = new SnowDateIdGenerator();
```

`HasValueGenerator<SnowDateIdGenerator>()` 读取该单例。未赋值时抛 `IdGenerator instance not set`。设计时工厂同样赋值，否则 `dotnet ef` 建模型失败。

业务代码直接取号：

```csharp
var id = Singleton<SnowDateIdGenerator>.Instance!.NewSequenceId();
var code = Singleton<SnowDateIdGenerator>.Instance!.NewId("SO", digits: 6);
```

`SnowDateIdGenerator` 在 Id 前拼日期，默认格式 `yyMMdd`。`sequenceBits` 默认 10，同一秒最多 1024 个。机器码默认按进程哈希取 0–15，同一秒最多 16 个副本。时钟回拨超过 10 秒抛异常。

`SnowIdGenerator` 不含日期前缀，纪元为 2024-01-01 UTC，机器码位数 8。各副本的 `sequenceBits` 必须相同。

## JSON

平台 JSON 用 `JsonUtils`，不用裸 `JsonSerializer`。

```csharp
var json = JsonUtils.Serialize(order);
var order = JsonUtils.Deserialize<Order>(json);
var bytes = JsonUtils.SerializeBytes(order);
```

`Serialize` 使用 `JsonUtils.Options`：camelCase、保留 null、忽略循环引用、数字可从字符串读入、属性名大小写不敏感。空字符串或空白反序列化返回 `default`。

`JsonUtils.DefaultOptions` 在写出时忽略 null。需要这套行为时显式传入。

## FluentClient

```csharp
services.AddFluentClient("https://oapi.dingtalk.com",
    configureHttpClient: builder => builder.AddHttpMessageHandler<AuthHandler>());
```

注册为单例 `IFluentClient`，底层走 `IHttpClientFactory`。

```csharp
var user = await client.Get("user/get")
    .WithArgument("userid", id)
    .WithHeader("Authorization", token)
    .As<UserDto>();

var created = await client.Post(body, "orders").AsApi<OrderDto>();
```

`Get` / `Post` / `Put` / `Delete` 的第二个参数是相对 `BaseUrl` 的路径。查询参数用 `WithArgument` 或 `WithArguments(匿名对象)`。JSON 正文用 `WithJsonBody`。

取值：`As<T>`、`AsJson<T>`、`AsString`、`AsByteArray`、`AsStream`。`AsApi<T>` 按 `ResponseResult<T>` 解包，`Code != 200` 时抛 `ApiException`。

`RequestOptions.IgnoreHttpErrors` 默认 true：HTTP 非 2xx 仍返回响应体。设为 false 时，非成功状态码在读 body 前失败。

## 字符串与集合

`string.IsNull()` 为 null、空或空白。`IsEmpty()` 只判断 null 或空。`IsNotNull()` / `IsNotEmpty()` 相反。这四个方法带 `[InjectLambda]`，可写进 EF 表达式。

字符串转值：`ToInt32` / `ToInt64` / `ToDecimal` / `ToDateTime` / `ToDateTimeOffset` / `ToGuid` / `ToEnum<T>`。解析失败返回默认值；`ToGuid` 失败抛异常。

```csharp
query = query.WhereIf(code.IsNotNull(), x => x.Code == code);
var page = query.Paged(pageIndex: 0, pageSize: 20, out var total);
```

集合 `IsNull()` 表示 null 或没有元素。`WhereSafe` 在谓词为 null 时原样返回。`ContainsAny` / `ContainsAll` 判断集合是否包含给定值。`JoinStr()` 默认用逗号拼接。

`In` / `NotIn` 判断值是否在参数列表中：

```csharp
if (status.In(OrderStatus.Created, OrderStatus.Picking)) { }
```

枚举：`Description()` 读 `[Description]`，`Display()` 读 `[Display(Name)]`。没有特性时返回空字符串。

日期：`ToDateOnly()`、`FirstDayOfMonth` / `LastDayOfMonth`、`Timestamp`（秒）、`TimestampMs`。

表达式组合：

```csharp
Expression<Func<Order, bool>>? predicate = null;
predicate = predicate.And(x => x.WarehouseId == warehouseId);
predicate = predicate.AndIf(status.HasValue, x => x.Status == status);
predicate = predicate.Or(x => x.Code, keyword, ExprOperator.Contains);
```

`[ExprProperty(ExprOperator.Equal, FieldName = "Code")]` 标在查询 DTO 属性上，供表达式构建读取字段名和操作符。`[ExprIgnore]` 排除该属性。

## 反射与克隆

```csharp
var attr = property.GetReflector().GetCustomAttribute<ExprPropertyAttribute>();
var value = property.GetReflector().GetValue(entity);
var clone = DeepCloner.DeepClone(order);
var copy = DeepCloner.ShallowClone(order);
```

`GetReflector()` 可用于 `Type`、`PropertyInfo`、`MethodInfo`、`FieldInfo`、`ConstructorInfo`。方法调用：`method.GetReflector().Invoke(instance, args)`。特性读取走缓存，重复反射不每次 `GetCustomAttributes`。

`DeepClone` 复制引用图。`ShallowClone` 只复制一层。

扫描实现类：`TypeUtils.FindClassesOfType<T>(assemblies)`，默认只要具体类。

## Cron 与连接串

```csharp
CronExpression.IsValidExpression("0 0 2 * * ?");
var cron = new CronExpression("0 0 2 * * ?");
var next = cron.GetNextValidTimeAfter(DateTimeOffset.Now);
```

表达式含秒字段。`GetNextValidTimeAfter` 没有下一次时返回 null。

连接串风格的键值转对象：

```csharp
var options = ObjectUtils.ParseOptions<RedisOptions>("host=127.0.0.1,defaultDatabase=8");
```

按 `key=value` 拆分，分隔符默认 `,;`。属性名大小写不敏感。未知键默认忽略。`[Options("configuration")]` 增加别名，`[OptionsIgnore]` 排除属性。
