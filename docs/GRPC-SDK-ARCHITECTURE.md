# BeniceSoft gRPC SDK 架构设计

> 本文档描述在现有 **ABP Application.Contracts + HTTP 动态代理 + YARP 网关** 体系之上，引入 **编译期 proto 生成 + 独立 Grpc.Sdk NuGet** 的完整架构方案。  
> 短期继续 HTTP 远程调用；长期通过 `IIncrementalGenerator` 自动生成 gRPC 客户端，与 HTTP SDK **共用同一套契约、分包发布**。  
> **服务间 gRPC 仅采用 Direct 直连**：数据面不经过 Ingress 转发；Gateway Admin 仅作服务发现（控制面）。

---

## 1. 背景与目标

### 1.1 现状

| 组件 | 作用 |
|------|------|
| `Application.Contracts` | 定义 `IApplicationService` 接口与 DTO，是跨服务契约的唯一来源 |
| `*.Sdk` + `AddHttpClientProxies` | 扫描 Contracts 程序集，运行时生成 HTTP 动态代理 |
| `BeniceSoft.Abp.Http.Client` | 自定义 `IProxyHttpClientFactory`，透传 Authorization、IgnoreJsonFormat 等头 |
| `RemoteServices.BaseUrl` | 指向 **被调服务自身入口**（本机端口或 K8s `svc`）。ABP 动态代理会先请求 `{BaseUrl}/api/abp/api-definition`，必须落到该服务，不能配共享 Ingress |
| Gateway Admin | 服务注册、多实例 Destinations、负载均衡、健康检查 |
| 网关 Ingress | 对外 **HTTP/HTTPS**（浏览器、Swagger、外部 REST），**不是** ABP HTTP 服务间调用的 BaseUrl |

### 1.2 目标

1. **短期**：服务间继续 ABP HTTP 远程调用，`RemoteServices:{Name}:BaseUrl` 配被调服务地址即可，业务代码不改。
2. **中长期**：引入 gRPC 服务间通信，复用 **Contracts 同一批接口**，发布 **独立 `*.Grpc.Sdk` NuGet**,`BeniceSoft.Grpc.Generator`（`IIncrementalGenerator`）在 **Grpc.Sdk 编译期** 生成 proto / Client / Adapter，CI 发包；消费方按需引用 HTTP Sdk 或 Grpc.Sdk。

### 1.3 网关职责划分（HTTP vs gRPC）

| 流量类型 | 数据路径 | 使用的网关组件 |
|----------|----------|----------------|
| 浏览器 / 外部 HTTP | 客户端 → **Ingress** → YARP → 后端 REST | Ingress（数据面） |
| 服务间 ABP HTTP 远程调用 | 客户端 → **直连被调服务**（`RemoteServices.BaseUrl`） | 不经 Ingress； K8s 下由 Service 做 LB |
| 服务间 gRPC | 客户端 → **直连后端实例**（h2c） | Admin（**仅控制面**：查实例列表） |
| 服务注册 | 后端启动 → Admin `register` | Admin（控制面） |

**设计决策**：既然选用 gRPC，服务间调用 **不再经 Ingress 转发**。否则多一跳代理、HTTP/2 终止与重建，抵消 gRPC 多路复用与低延迟优势。Ingress 继续专职 **HTTP 南北向流量**（浏览器 / 外部）。ABP HTTP 动态代理同样 **直连被调服务**：客户端会请求 `{BaseUrl}/api/abp/api-definition`（路径写死），共享 Ingress 无法把该地址按服务拆开，因此不能把所有服务的 `BaseUrl` 配成同一个网关。

---

## 2. 核心设计原则

### 2.1 契约单一来源（SSOT）

**远程可调用的边界 = `Application.Contracts` 程序集中继承 `IApplicationService` 的接口。**

这与 ABP `AddHttpClientProxies(typeof(ApplicationContractsModule).Assembly, ...)` 的扫描范围一致：

- 接口 **定义在** Contracts → HTTP 代理可生成 → gRPC **也应**生成。
- 接口只在 Application 层、`AppService` 继承「空接口」且 Contracts 无定义 → SDK **不会**包含该接口 → HTTP / gRPC **都不会**有。

因此：**HTTP 与 gRPC 共用同一套扫描规则，无需两套 Opt-in 标记（默认全生成，少数 Opt-out）。**

### 2.2 生成一次，Sdk 发包

```
服务端仓库（如 Wecharmer.AM）
  Application.Contracts
       ↓ ProjectReference
  Wecharmer.AM.Grpc.Sdk  +  BeniceSoft.Grpc.Generator
       ↓ 编译期生成 proto / Client / Adapter
  dotnet pack → NuGet（Wecharmer.AM.Grpc.Sdk）

消费方（如 Wecharmer.PermissionCenter）
  PackageReference Wecharmer.AM.Grpc.Sdk   ← 仅用 gRPC 时
  PackageReference Wecharmer.AM.Sdk        ← 仅用 HTTP 时（现有）
  ❌ 不引用 Generator，不重新生成 AM 的 proto
  ✅ 直接使用 Grpc.Sdk 包内 Client + Adapter
```

### 2.3 分包发布：HTTP Sdk 与 Grpc.Sdk 独立

**HTTP 与 gRPC 不混在同一 NuGet**，消费方按需引用：

| 包 | 内容 | 何时引用 |
|----|------|----------|
| `Wecharmer.AM.Sdk` | Contracts（传递）+ `AddHttpClientProxies` + HTTP 依赖 | 服务间走 ABP HTTP 远程调用 |
| `Wecharmer.AM.Grpc.Sdk` | Contracts（传递）+ gRPC Client/Adapter + `BeniceSoft.Grpc.Runtime` | 服务间走 gRPC |

| 原则 | 说明 |
|------|------|
| 契约共用 | 两者扫描 **同一** `Application.Contracts` 程序集 |
| 包独立 | HTTP Sdk **不**引用 Grpc.Generator / Grpc.Runtime；Grpc.Sdk **不**引用 `BeniceSoft.Abp.Http.Client` |
| 依赖轻量 | 只用 gRPC 的服务不拉 HTTP 客户端栈，反之亦然 |
| 可同时引用 | 迁移期允许两个包并存，但 **同一接口** 在 DI 中只注册一种实现（HTTP 或 gRPC） |

```
Wecharmer.AM.Application.Contracts
        │
        ├──────────────────────┬──────────────────────┐
        ▼                      ▼                      │
 Wecharmer.AM.Sdk      Wecharmer.AM.Grpc.Sdk           │
 (HTTP 动态代理)         (Generator + gRPC Client)      │
        │                      │                      │
        └────────── 消费方按需引用其一或迁移期双引用 ────┘
```

### 2.4 Generator 挂在 Grpc.Sdk，不挂在 Contracts

| 挂载位置 | 说明 |
|----------|------|
| `Application.Contracts` | ❌ 保持纯 C# 接口 + DTO，不引入 gRPC 依赖 |
| `*.Grpc.Sdk` | ✅ 编译时读取 Contracts 程序集符号，生成物打入 **Grpc.Sdk** NuGet |

### 2.5 gRPC 不需要 ABP `RemoteServices` 配置

HTTP Sdk 与 Grpc.Sdk 的 **配置模型不同**：

| | HTTP Sdk（ABP） | Grpc.Sdk |
|---|----------------|----------|
| 配置节 | `RemoteServices:{Name}:BaseUrl` | **不用** `RemoteServices` |
| 目标地址 | 每个远程服务配 **该服务自身入口**（本机 `http://localhost:端口/`，或 K8s `<服务名>.<命名空间>.svc.cluster.local:<端口>`） | 服务名编译期写入 Sdk；运行时 **Admin 发现实例后直连** |
| 多实例负载均衡 | K8s Service / 被调服务入口 | Sdk 内 Resolver + **客户端 LB** |

Grpc.Sdk 运行时只需 **全局基础设施配置**（一次），无需为每个被调服务写 `BaseUrl`：

```json
{
  "Grpc": {
    "GatewayDiscoveryBaseUrl": "http://192.168.5.226:5188",
    "EnableHttp2Unencrypted": true,
    "DeadlineSeconds": 30
  }
}
```

| 配置项 | 必填 | 说明 |
|--------|------|------|
| `GatewayDiscoveryBaseUrl` | **是** | Gateway **Admin** 地址，拉取实例列表（控制面） |
| `EnableHttp2Unencrypted` | 否 | 内网 `http://` 实例 h2c，默认 `true` |
| `DeadlineSeconds` | 否 | RPC 超时，默认 30 |
| `ServiceOverrides` | 否 | 本地调试按集群名覆盖固定地址（见 §2.6） |

**服务名（如 `wecharmer-am`）** 在 `AmGrpcSdkModule.RemoteServiceName` / 生成代码中 **硬编码**，与 Gateway 集群名、`ServiceDiscovery:ServiceName` 一致，**消费方不必再配**。

#### Direct 直连（唯一的服务间 gRPC 模式）

```
Permission gRPC Client
  → IGrpcServiceInstanceResolver
  → GET {GatewayDiscoveryBaseUrl}/api/service-discovery/services/wecharmer-am/instances
  → 得到 [http://10.0.0.1:6002, http://10.0.0.2:6002, ...]
  → Client-side 负载均衡 + h2c 直连实例
  → AM Kestrel gRPC Server（MapGrpcService）
```

- **数据面不经过 Ingress**；Admin 仅用于发现实例地址。
- **不需要** `RemoteServices:Wecharmer.AM:BaseUrl`。
- 多实例由 **Grpc.Sdk 内 Resolver + LB** 负责（与 YARP 集群 Destinations **共用同一注册数据**）。

> **说明**：是 **gRPC Client（消费方）** 查 Admin 后直连后端，不是 AM 上的 gRPC Server 去查实例。Server 只在本机 `MapGrpcService` 监听；启动时向 Admin **注册**本实例地址即可。

### 2.6 本地开发：`ServiceOverrides`（等价于 HTTP 的 RemoteServices 覆盖）

gRPC **不是**「完全零配置、也无法指定本地」。日常开发中「只起本服务、其它走测试环境」以及「两个服务本地联调」的需求，通过 **`Grpc:ServiceOverrides`** 实现，语义对齐 ABP HTTP 的 **按服务名单独改 BaseUrl**。

#### 与 HTTP 开发流程对照

| 开发场景 | HTTP（现状） | gRPC（设计） |
|----------|-------------|--------------|
| 本机起仓储，Swagger 调仓储，其它服务走测试环境 | 各 `RemoteServices:*:BaseUrl` 指向 **测试环境该服务的 svc / 直连地址** | 不配 Override；Resolver 查 Gateway Admin → 测试环境实例 |
| 仓储 + 商品档案都要本地联调 | 仅把 **商品档案** 的 BaseUrl 改为 `http://localhost:端口` | 仅把 **商品档案** 写入 `ServiceOverrides` → `http://localhost:端口` |
| 本机服务是否注册到测试 Gateway | 通常 **不注册** 或谨慎注册，避免污染测试路由 | 本地联调时 **建议不注册** 被 Override 的服务；Override 指向本机即可 |

**示例：仓储本地调试，商品档案改本机，其余走测试环境**

HTTP（`appsettings.Development.json`）：

```json
{
  "RemoteServices": {
    "Wecharmer.WMS": {
      "BaseUrl": "http://wecharmer-wms.wms-test.svc.cluster.local:80/"
    },
    "Wecharmer.Product": {
      "BaseUrl": "http://localhost:6010/"
    }
  }
}
```

- `BaseUrl` 必须是被调服务 **origin**（建议以 `/` 结尾），例如本机 `http://localhost:6003/`，集群内 `http://<服务名>.<命名空间>.svc.cluster.local:<端口>/`。
- **不要**写成共享 Ingress（如 `:5056`），也 **不要**带业务路由前缀（如 `/api/wms/`）。ABP `ApiDescriptionFinder` 固定请求 `{BaseUrl}/api/abp/api-definition`，前缀会拼错路径，Ingress 则会落到错误的后端。

gRPC（等价配置）：

```json
{
  "Grpc": {
    "GatewayDiscoveryBaseUrl": "http://192.168.5.226:5188",
    "ServiceOverrides": {
      "wecharmer-product": "http://localhost:6010"
    }
  }
}
```

- `wecharmer-product`：Gateway **集群名** / `ServiceDiscovery:ServiceName`，与 Grpc.Sdk 内编译的服务名一致。
- 未出现在 `ServiceOverrides` 中的服务（如 `wecharmer-am`、`wecharmer-permission`）：仍走 Gateway Admin 实例发现 → **测试环境**。

#### Resolver 解析优先级

`IGrpcServiceInstanceResolver` 默认实现（`CompositeGrpcServiceInstanceResolver`）按以下顺序：

```
1. Grpc:ServiceOverrides:{serviceName}  → 固定地址（本地调试，最高优先级）
2. Gateway Admin GET .../instances      → 测试/生产已注册实例（默认）
```

```csharp
// BeniceSoft.Grpc.Runtime 伪代码
public async Task<IReadOnlyList<Uri>> ResolveAsync(string serviceName, CancellationToken ct)
{
    if (_options.ServiceOverrides.TryGetValue(serviceName, out var overrideAddress))
        return [new Uri(overrideAddress)];  // 仅本机，不参与 LB

    return await _gatewayAdminClient.GetInstancesAsync(serviceName, ct);
}
```

#### 本地联调注意事项

| 做法 | 结果 | 建议 |
|------|------|------|
| 本地商品档案 **不注册** Gateway + `ServiceOverrides` 指本机 | 稳定连本机 | ✅ **推荐** |
| 本地商品档案 **注册**到测试 Gateway（同名集群） | 实例列表混有本机 + 测试机，LB 可能打到测试环境 | ❌ 避免 |
| 关闭本服务自动注册 | `ServiceDiscovery:EnableAutoRegistration: false` | 仅本地调试、不想污染测试注册表时 |

#### 迁移期 HTTP 与 gRPC 混用

尚未全量切 gRPC 时，可按服务拆分：

- 商品档案：仍用 HTTP Sdk + `RemoteServices` 指本机  
- 其它服务：已用 Grpc.Sdk + Admin 发现 + 直连  

不必一次全部改为 gRPC。

---

## 3. 总体架构

### 3.1 逻辑分层

```
┌─────────────────────────────────────────────────────────────────┐
│                    Application.Contracts                         │
│         IUserAppService : IApplicationService + DTOs             │
└────────────────────────────┬────────────────────────────────────┘
                             │
         ┌───────────────────┴───────────────────┐
         ▼                                       ▼
┌─────────────────────┐               ┌─────────────────────┐
│  ABP HTTP 动态代理   │               │ BeniceSoft.Grpc.     │
│  (运行时反射)        │               │ Generator (编译期)    │
│  JSON over HTTP      │               │ proto + Client +     │
└──────────┬──────────┘               │ Adapter              │
           │                          └──────────┬──────────┘
           ▼                                     ▼
┌─────────────────────┐               ┌─────────────────────┐
│  Wecharmer.AM.Sdk   │               │ Wecharmer.AM.Grpc.Sdk│
│  (HTTP NuGet)       │               │ (gRPC NuGet)         │
└──────────┬──────────┘               └──────────┬──────────┘
           │                                     │
           └────────────── 消费方按需引用 ────────┘
                          │
           ┌──────────────┴──────────────┐
           ▼                             ▼
    消费方 DI 注入                  服务端 Web 宿主
    IUserAppService                 MapGrpcService
    (HTTP 代理 或 GrpcAdapter)      → 转调 AppService
```

### 3.2 运行时调用链

**HTTP（当前，直连被调服务）：**

```
消费方 AppService
  → IUserAppService (HTTP 动态代理)
  → GET {BaseUrl}/api/abp/api-definition  （发现真实业务 URL，含 RootPath）
  → HttpClient → 被调服务 REST（如 /api/am/...）
```

**gRPC（Direct，数据面直连）：**

```
消费方 AppService
  → IUserAppService (GrpcAdapter)
  → GrpcChannelFactory（Admin 查实例列表 + 客户端 LB）
  → h2c 直连后端实例 → gRPC Server → 本地 AppService
```

### 3.3 与 YARP 网关的关系

| 场景 | 客户端配置 | 数据路径 | 多实例 LB | RemoteServices |
|------|------------|----------|-----------|----------------|
| HTTP 服务间 | `RemoteServices:{Name}:BaseUrl` → **被调服务** | **直连**该服务（含 `/api/abp/api-definition`） | K8s Service | **是** |
| gRPC 服务间 | `Grpc:GatewayDiscoveryBaseUrl` → Admin | **直连实例** | Sdk 客户端 LB | **否** |
| 浏览器 / 外部 | Ingress HTTPS | 经 Ingress + YARP | YARP | 不适用 |

Gateway Admin 实例 API（现有）：

```
GET /api/service-discovery/services/{serviceName}/instances
→ [{ "instanceId", "address", "metadata" }, ...]
```

Grpc.Sdk 的 `IGrpcServiceInstanceResolver` 调用上述 API，与 HTTP 服务注册 **共用同一注册中心数据**；**不要求** Ingress 配置 gRPC 转发路由。

---

## 4. 项目结构规划

### 4.1 BeniceSoft 框架层（新建）

```
BeniceSoft.Abp/
├── BeniceSoft.Grpc.Abstractions/       # 特性、选项、运行时抽象接口
├── BeniceSoft.Grpc.Generator/          # IIncrementalGenerator（Analyzer 包）
├── BeniceSoft.Grpc.Runtime/            # ChannelFactory、Resolver、Interceptor、DI 扩展
└── BeniceSoft.Grpc.Generator.Tests/    # 快照测试、类型映射单测
```

### 4.2 业务服务层（以 Wecharmer.AM 为例）

```
Wecharmer.AM/
├── Wecharmer.AM.Application.Contracts/   # 现有，接口 + DTO
├── Wecharmer.AM.Sdk/                       # 现有，仅 HTTP（AddHttpClientProxies）
│   └── AmSdkModule.cs
├── Wecharmer.AM.Grpc.Sdk/                  # 新建，仅 gRPC
│   ├── AmGrpcSdkModule.cs                  # AddAmGrpcClients
│   └── （编译生成物进入此项目输出）
└── Wecharmer.AM.Web/                       # AddGrpc + MapGrpcService + 转调 AppService
```

### 4.3 NuGet 包关系

| 包 | 内容 | 引用方 |
|----|------|--------|
| `Wecharmer.AM.Application.Contracts` | 接口 + DTO | 可选直接引用；Sdk / Grpc.Sdk 均传递依赖 |
| `Wecharmer.AM.Sdk` | Contracts + HTTP 动态代理 | 仅用 HTTP 远程调用时 |
| `Wecharmer.AM.Grpc.Sdk` | Contracts + gRPC Client/Adapter + Runtime | 仅用 gRPC 远程调用时 |
| `BeniceSoft.Grpc.Generator` | 仅 Analyzer | **Grpc.Sdk**（PrivateAssets） |
| `BeniceSoft.Grpc.Runtime` | 通道、发现、拦截器 | **Grpc.Sdk** |

---

## 5. 契约扫描与生成规则

### 5.1 扫描范围（与 ABP HTTP 对齐）

Generator 在 **Sdk 编译时** 解析引用的 Contracts 程序集：

```csharp
// 纳入生成
public interface IUserAppService : IApplicationService { ... }

// 不纳入（不在 Contracts 或无 IApplicationService）
internal interface IInternalService { ... }
```

### 5.2 Opt-out：`[GrpcExcluded]`

默认 **Contracts 内所有 `IApplicationService` 均生成 gRPC**。以下情况使用 Opt-out：

```csharp
[GrpcExcluded("文件流接口，仅 HTTP")]
public interface IFileAppService : IApplicationService
{
    Task<IRemoteStreamContent> GetStreamAsync(...);
}
```

典型排除场景：大文件流、暂时无法映射 proto 的 DTO、仅浏览器使用的页面型 API。

### 5.3 方法级规则（Generator v1）

| 规则 | 处理 |
|------|------|
| `Task<T>` / `Task` | 支持 |
| `CancellationToken` | 从签名剥离，不进 proto |
| 默认参数 | 生成 proto 时固化默认值或禁止（v1 建议禁止并 Diagnostic） |
| `IRemoteStreamContent` | v1 排除或 `[GrpcExcluded]` |
| 复杂泛型 / `object` | 编译报错（Diagnostic） |

### 5.4 命名约定（建议）

| 元素 | 规则 | 示例 |
|------|------|------|
| proto package | `{company}.{service}` 小写 | `wecharmer.am` |
| proto service | 接口名去掉 `I` 后缀 | `UserAppService` |
| rpc 方法 | 与 C# 方法同名 PascalCase → proto camelCase | `GetAsync` → `getAsync` |
| C# Adapter | `{InterfaceName}GrpcAdapter` | `UserAppServiceGrpcAdapter` |

### 5.5 类型映射表（v1 基线）

| C# | proto | 备注 |
|----|-------|------|
| `int` / `long` | `int32` / `int64` | |
| `string` | `string` | |
| `bool` | `bool` | |
| `byte[]` | `bytes` | |
| `Guid` | `string` | 统一 string，避免跨语言差异 |
| `DateTime` / `DateTimeOffset` | `int64` 或 `string` | **选定一种，全项目统一** |
| `decimal` | `string` 或 `google.type.Decimal` | v1 建议 string |
| `enum` | `enum` | |
| `List<T>` / `T[]` | `repeated T` | |
| `T?`（可空值类型） | `optional`（proto3） | |
| 嵌套 DTO | `message` | 递归展开 |
| `PagedResultDto<T>` | 专用 message 模板 | 生成器内置 |

---

## 6. 编译期流水线

### 6.1 Grpc.Sdk 项目内 MSBuild 流程

```
1. Wecharmer.AM.Grpc.Sdk 编译启动
2. BeniceSoft.Grpc.Generator（IIncrementalGenerator）
   - 读取 Contracts 程序集符号
   - 输出 .proto 到 $(IntermediateOutputPath)/generated/grpc/
   - 输出 Adapter 源码 via context.AddSource
3. Grpc.Tools 编译 .proto → *Grpc.cs（Client + Server base）
4. （可选）第二轮生成 Server 薄封装 / 注册扩展
5. Grpc.Sdk 程序集打包 → NuGet（Wecharmer.AM.Grpc.Sdk）
```

### 6.2 Grpc.Sdk 项目 csproj 参考（目标形态）

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsPackable>true</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="BeniceSoft.Grpc.Runtime" />
    <PackageReference Include="BeniceSoft.Grpc.Generator"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
    <PackageReference Include="Grpc.Tools" PrivateAssets="All" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Wecharmer.AM.Application.Contracts\..." />
  </ItemGroup>

  <ItemGroup>
    <Protobuf Include="$(IntermediateOutputPath)generated/grpc/**/*.proto"
              GrpcServices="Client" />
  </ItemGroup>
</Project>
```

> `Wecharmer.AM.Sdk` 保持现有结构，**不**引用 Grpc 相关包。

---

## 7. 运行时组件（BeniceSoft.Grpc.Runtime）

### 7.1 核心接口

```csharp
public interface IGrpcChannelFactory
{
    GrpcChannel GetChannel(string remoteServiceName);
}

/// <summary>
/// 解析目标服务实例地址。优先级：ServiceOverrides → Gateway Admin 实例列表。
/// </summary>
public interface IGrpcServiceInstanceResolver
{
    Task<IReadOnlyList<Uri>> ResolveAsync(string serviceName, CancellationToken ct);
}
```

`IGrpcServiceInstanceResolver` 默认实现调用 **Gateway Admin 服务发现 API**，并支持 **`ServiceOverrides` 本地覆盖**（见 §2.6）。

### 7.2 Interceptor（对齐 BeniceSoft HTTP 行为）

| Interceptor | 作用 |
|-------------|------|
| `AuthInterceptor` | 透传 Bearer Token（等价 HTTP `BeniceSoftProxyHttpClientFactory`） |
| `TenantInterceptor` | 透传 TenantId |
| `CorrelationIdInterceptor` | 透传 CorrelationId |
| `AbpExceptionInterceptor` | `RpcException` ↔ `AbpRemoteCallException` / 业务异常 |

### 7.3 DI 扩展（Grpc.Sdk Module 内）

```csharp
// Wecharmer.AM.Grpc.Sdk / AmGrpcSdkModule.cs
public static IServiceCollection AddAmGrpcSdk(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // 读取 Grpc:GatewayDiscoveryBaseUrl、ServiceOverrides 等
    // 注册 ChannelFactory、GatewayAdminInstanceResolver、Interceptors
    // 注册各 XxxAppServiceGrpcAdapter → IXxxAppService
    return services;
}
```

### 7.4 配置项（全局 `Grpc` 节）

| 选项 | 必填 | 说明 |
|------|------|------|
| `GatewayDiscoveryBaseUrl` | **是** | Gateway Admin（如 `:5188`），拉 `/api/service-discovery/services/{name}/instances` |
| `EnableHttp2Unencrypted` | 否 | 内网 `http://` 实例 h2c，默认 `true` |
| `DeadlineSeconds` | 否 | RPC 超时，默认 30 |
| `ServiceOverrides` | 否 | 按 **Gateway 集群名** 覆盖为固定地址；本地调试用（见 §2.6） |
| `InstanceCacheSeconds` | 否 | Admin 实例列表缓存时间，默认 30（实现阶段可调） |

**不需要配置的内容：**

- ❌ `RemoteServices:Wecharmer.AM:BaseUrl`（gRPC 不用 ABP 远程服务 URL）
- ❌ `GatewayIngressAddress`（gRPC 数据面不经 Ingress）
- ❌ 每个被调服务的实例地址（运行时从 Admin 拉取，或 `ServiceOverrides` 覆盖）
- ❌ 被调服务名（编译在 `AmGrpcSdkModule.RemoteServiceName = "wecharmer-am"`）

**与现有 `ServiceDiscovery` 的关系：**

| 角色 | 配置 | 用途 |
|------|------|------|
| 服务 **提供方**（AM 启动） | `ServiceDiscovery:GatewayBaseUrl` → Admin | **注册**本实例 |
| 服务 **消费方**（Permission 调 AM） | `Grpc:GatewayDiscoveryBaseUrl` → Admin | **查询** AM 实例列表后直连 |

两者都指向 Gateway Admin，职责不同（注册 vs 发现），可共用同一地址。

---

## 8. 服务端（Web 宿主）

### 8.1 Kestrel

```json
"Kestrel": {
  "EndpointDefaults": {
    "Protocols": "Http1AndHttp2"
  }
}
```

REST（浏览器 / HTTP 代理）与 gRPC（h2c 直连）**共用端口**；注册到 Admin 的 `address` 须包含该端口。

### 8.2 注册 gRPC

```csharp
builder.Services.AddGrpc();
app.MapGrpcService<UserAppServiceGrpcImpl>();
```

### 8.3 Server 实现模式（避免重复业务逻辑）

```csharp
// Grpc 入口只做协议适配，内部调用现有 AppService
public class UserAppServiceGrpcImpl : UserAppService.UserAppServiceBase
{
    private readonly IUserAppService _appService; // 本地真实实现，非代理

    public override async Task<UserDto> GetAsync(GetUserRequest request, ServerCallContext context)
        => await _appService.GetAsync(Guid.Parse(request.Id));
}
```

> 注意：服务端注入的 `IUserAppService` 必须是 **本地 AppService 实现**，不能是 HTTP/gRPC 客户端代理，避免循环调用。

### 8.4 h2c 与运行时开关

内网 `http://` 直连须启用明文 HTTP/2 支持（消费方与服务方宿主均需）：

```csharp
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
```

Ingress 侧若继续代理 **HTTP** 流量，同样保留该开关；**gRPC 服务间流量不经过 Ingress**，无需在 Ingress 上配置 gRPC 路由。

---

## 9. 消费方接入（迁移指南）

### 9.1 引用（按需）

```xml
<!-- 仅 HTTP -->
<PackageReference Include="Wecharmer.AM.Sdk" Version="x.y.z" />

<!-- 仅 gRPC -->
<PackageReference Include="Wecharmer.AM.Grpc.Sdk" Version="x.y.z" />

<!-- 迁移期可暂时两个都引，但同一接口 DI 只注册一种实现 -->
```

**不要**在消费方引用 `BeniceSoft.Grpc.Generator`。

### 9.2 模块依赖

**仅用 HTTP（现状）：**

```csharp
[DependsOn(typeof(AmSdkModule))]
public class PermissionCenterModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpRemoteServiceOptions>(options =>
        {
            options.RemoteServices.Configure("Wecharmer.AM", c =>
                c.BaseUrl = configuration["RemoteServices:Wecharmer.AM:BaseUrl"]!);
        });
    }
}
```

**仅用 gRPC：**

```csharp
[DependsOn(typeof(AmGrpcSdkModule))]
public class PermissionCenterModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAmGrpcSdk(configuration);
    }
}
```

`appsettings.json`（gRPC 消费方）：

```json
{
  "Grpc": {
    "GatewayDiscoveryBaseUrl": "http://192.168.5.226:5188"
  }
}
```

### 9.3 迁移期双栈

| 阶段 | 引用包 | IUserAppService 实现 | RemoteServices |
|------|--------|---------------------|----------------|
| 0 | `Wecharmer.AM.Sdk` | ABP HTTP 动态代理 | 需要 |
| 1 | Sdk + Grpc.Sdk | FeatureFlag 切换 GrpcAdapter | HTTP 仍需要 |
| 2 | 仅 `Wecharmer.AM.Grpc.Sdk` | GrpcAdapter | **可删除** AM 相关 RemoteServices |

### 9.4 本地调试场景速查

**场景 A：只起仓储，其它服务走测试环境（最常见）**

```json
{
  "Grpc": {
    "GatewayDiscoveryBaseUrl": "http://192.168.5.226:5188"
  }
}
```

- 不配 `ServiceOverrides`。
- 仓储本地 Swagger 调本机 API；仓储内远程调用经 Grpc Client 查 Admin → 测试环境实例 → **直连**。
- 本机仓储 **可不向测试 Gateway 注册**（`EnableAutoRegistration: false`），避免测试流量打到开发机。

**场景 B：仓储 + 商品档案本地联调，其它仍走测试**

```json
{
  "Grpc": {
    "GatewayDiscoveryBaseUrl": "http://192.168.5.226:5188",
    "ServiceOverrides": {
      "wecharmer-product": "http://localhost:6010"
    }
  }
}
```

- 等价于 HTTP 里只改商品档案的 `RemoteServices:BaseUrl`。
- 本地商品档案进程 **不要** 向测试 Gateway 注册同名服务（除非明确需要混合 LB）。

**场景 C：全部依赖项都在测试环境**

- `GatewayDiscoveryBaseUrl` 指向测试 Admin 即可；Resolver 拉实例后直连。

**场景 D：迁移期部分服务仍用 HTTP**

```json
{
  "RemoteServices": {
    "Wecharmer.Product": { "BaseUrl": "http://localhost:6010/" }
  },
  "Grpc": {
    "GatewayDiscoveryBaseUrl": "http://192.168.5.226:5188"
  }
}
```

- 已切 gRPC 的服务：Admin 发现 + 直连。
- 未切的服务：继续 HTTP + `RemoteServices`（直连被调服务，本机端口或 K8s svc）。

---

## 10. 版本与兼容

### 10.1 版本号策略

- `Application.Contracts`、`Wecharmer.AM.Sdk`、`Wecharmer.AM.Grpc.Sdk` 版本 **联动发布**（Grpc.Sdk 依赖 Contracts 版本）。
- Contracts 仅新增 DTO 字段 / 新方法 → **minor** bump（两个 Sdk 同步）。
- 删除或修改 proto field number → **major** bump（Grpc.Sdk；HTTP Sdk 视 JSON 兼容性而定）。

### 10.2 proto 兼容规则

1. 已发布 field number **永不复用、永不改类型**。
2. 新字段使用新 number；旧字段标记 `deprecated`。
3. CI 运行 **golden proto 快照测试**，防止意外 diff。

### 10.3 生成器版本

`BeniceSoft.Grpc.Generator` 版本独立于 Sdk；Sdk csproj 应 **锁定 Generator 最低版本**，避免不同 CI 机器生成结果不一致。

---

## 11. 测试策略

| 层级 | 内容 |
|------|------|
| Generator 单测 | 给定 sample Contracts → 断言 proto / Adapter 快照 |
| Sdk 集成测 | TestServer 起 gRPC → Adapter 调用 → 断言 DTO |
| 契约一致性 | 同一接口 HTTP 与 gRPC 返回结构对比（双跑测试） |
| 多实例 | Resolver 返回多地址 → 客户端 LB 轮询 |
| 鉴权 | Token 透传后后端 `[Authorize]` 通过 |
| 直连 | 验证不经 Ingress，Admin 发现地址与注册 address 一致 |

---

## 12. 实施路线图

### 阶段 0：现状维持 ✅

- [x] 服务间 HTTP：`RemoteServices.BaseUrl` 指向被调服务自身（本机端口或 K8s svc），**不是**共享 Ingress
- [x] 后端 Kestrel `Http1AndHttp2`（为 gRPC 监听做准备）
- [x] Gateway Admin 实例 API + 服务自动注册
- [x] `Http2UnencryptedSupport`（h2c）

### 阶段 1：框架基础（Generator + Runtime）

- [ ] 创建 `BeniceSoft.Grpc.Abstractions`（`[GrpcExcluded]`、`GrpcOptions`、接口）
- [ ] 创建 `BeniceSoft.Grpc.Generator`（IIncrementalGenerator v1）
  - [ ] 扫描 `IApplicationService` 接口
  - [ ] 生成 `.proto`
  - [ ] 集成 `Grpc.Tools`
  - [ ] 生成 `*GrpcAdapter`
  - [ ] Diagnostic 与类型映射表
- [ ] 创建 `BeniceSoft.Grpc.Runtime`
  - [ ] `IGrpcChannelFactory`
  - [ ] `IGrpcServiceInstanceResolver` + `CompositeGrpcServiceInstanceResolver`（Override → Admin 发现）
  - [ ] 客户端负载均衡（RoundRobin / PickFirst，v1 二选一）
  - [ ] `GrpcOptions` 配置绑定（含 `ServiceOverrides`、实例缓存）
  - [ ] Interceptors（Auth / Tenant / CorrelationId / Exception）
  - [ ] `AddXxxGrpcSdk` DI 模板
- [ ] 创建 `BeniceSoft.Grpc.Generator.Tests`（快照测试）

### 阶段 2：AM 试点

- [ ] 新建 `Wecharmer.AM.Grpc.Sdk`（引用 Contracts + Generator + Runtime）
- [ ] `AmGrpcSdkModule` + `AddAmGrpcSdk`
- [ ] `Wecharmer.AM.Sdk` **保持仅 HTTP，不改动**
- [ ] `Wecharmer.AM.Web` 增加 `AddGrpc` + 1～2 个 `MapGrpcService` 试点
- [ ] 注册实例 address 与 gRPC 监听端口一致
- [ ] CI 发布 `Wecharmer.AM.Grpc.Sdk` NuGet
- [ ] 压测：对比 HTTP（直连被调服务）vs gRPC Direct 延迟与吞吐

### 阶段 3：Permission 消费方

- [ ] 引用 `Wecharmer.AM.Grpc.Sdk`（HTTP 仍用则保留 `Wecharmer.AM.Sdk`）
- [ ] 配置 `Grpc:GatewayDiscoveryBaseUrl`，**不配** `RemoteServices:Wecharmer.AM`（若已全量 gRPC）
- [ ] FeatureFlag 切换 `IUserAppService` → GrpcAdapter
- [ ] 验证鉴权、租户、异常映射、多实例 LB

### 阶段 4：推广与 v2

- [ ] Permission / Workflow / 其他服务复制 Grpc.Sdk 模式
- [ ] Generator v2：server streaming、大文件、`oneof`
- [ ] 文档：新服务接入 Checklist（含 Admin 注册与 h2c 检查项）
- [ ] 可选：导出 proto 供 Go 等非 .NET 消费方（跨语言场景）

---

## 13. 常见问题（FAQ）

### Q1：Permission 引用 AM Grpc.Sdk 时，要在 Permission 里再跑 Generator 吗？

**不要。** AM 的 proto 在 **AM.Grpc.Sdk 打包时** 已生成。Permission 只引用 `Wecharmer.AM.Grpc.Sdk`。

### Q2：用 gRPC Sdk 还要配 `RemoteServices` 吗？

**不要。** gRPC 不走 ABP HTTP 远程服务配置。只需全局 `Grpc:GatewayDiscoveryBaseUrl`（及可选 `ServiceOverrides`）。被调服务名已编译进 Grpc.Sdk；多实例由 Admin 发现 + Sdk 客户端 LB + **直连** 解决。

### Q3：gRPC 为什么不经 Ingress 转发？

已选用 gRPC 是为了 **二进制序列化、HTTP/2 多路复用、少一跳延迟**。经 Ingress 再代理一层会终止并重建 HTTP/2，收益大打折扣。Ingress 保留给 **HTTP 南北向**（浏览器 / 外部）。服务间无论 HTTP 动态代理还是 gRPC，数据面都 **直连被调服务 / 实例**。

### Q4：本地只起仓储、其它走测试环境，gRPC 能做到吗？

**能。** 默认 Resolver 查 Gateway Admin；某个服务要本地联调时用 `Grpc:ServiceOverrides`。详见 §2.6、§9.4。

### Q5：Contracts 里所有接口都生成 gRPC 会不会太多？

在 ABP 约定下，Contracts 本身就是远程契约面；与 HTTP 代理范围一致。少数例外用 `[GrpcExcluded]`。

### Q6：gRPC Direct 比 HTTP 直连快多少？

预期 **Protobuf + HTTP/2 连接复用** 带来收益（高 QPS、小中 payload 更明显），具体需 AM 试点压测。低 QPS 管理类调用差异可能不大。两者数据面都不经 Ingress。

### Q7：和 Dapr 的关系？

Dapr 是 Sidecar 级服务治理，与本文 **Sdk + Generator + Gateway Admin 发现** 路线独立。短期不采用 Dapr 做服务调用。

### Q8：前端 / 外部调用怎么办？

继续走 **HTTP REST + Ingress HTTPS**。gRPC 仅用于 **.NET 服务间**；外部 OpenAPI/Swagger 不变。

---

## 14. 参考：现有相关代码

| 路径 | 说明 |
|------|------|
| `BeniceSoft.Abp/src/BeniceSoft.Abp.Http.Client/` | HTTP 动态代理、`BeniceSoftProxyHttpClientFactory` |
| `BeniceSoft.Abp/src/BeniceSoft.Abp.Dapr.Client/` | Dapr 集成（薄封装，当前未用于服务间调用） |
| `Wecharmer.AM/src/Wecharmer.AM.Sdk/AmSdkModule.cs` | `AddHttpClientProxies` 扫描 Contracts |
| `Gateway/` | YARP Ingress（HTTP）、Admin 服务发现与注册 |

---

## 15. 文档维护

| 版本 | 日期 | 说明 |
|------|------|------|
| 0.1 | 2026-06-25 | 初稿：架构原则、项目结构、实施路线图 |
| 0.2 | 2026-06-25 | HTTP Sdk / Grpc.Sdk 分包；gRPC 不用 RemoteServices；Gateway 注册中心发现实例 |
| 0.3 | 2026-06-25 | §2.6 本地开发与 ServiceOverrides；§9.4 本地调试场景 |
| 0.4 | 2026-06-29 | **移除 ViaGateway**；服务间 gRPC 仅 Direct；明确 Admin 控制面 / Ingress HTTP 数据面分工 |
| 0.5 | 2026-08-17 | 修正阶段 0 / HTTP `RemoteServices.BaseUrl`：指向被调服务（本机或 K8s svc），不是共享 Ingress；Ingress 仅南北向 HTTP |

后续实现过程中，每完成一个阶段请更新本文档对应 Checkbox 与「文档维护」版本表。
