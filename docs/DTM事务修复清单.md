# DTM TCC 和 Saga 修复清单

本清单跟踪框架缺陷修复、可持久化样例补全及真实测试环境复验。Saga 仅补偿已注册分支，不自动回滚发起方任意已提交数据；样例需把订单状态变化纳入可补偿分支。

## 修复项

- [x] TCC 全局生命周期：Prepare 成功后执行 Try，业务成功后 Submit，业务异常或取消后 Abort；Submit 响应不确定时不擅自改为取消；Abort 失败保留原异常和调度所需信息
- [x] HTTP 分支调用：实现分支注册和 Try 请求，正确携带 DTM 参数、分支请求头及业务参数，并检查响应
- [x] 回调屏障注册：明确注册 TCC、Saga 中间件需要的专用接口
- [x] 完整子事务屏障：Cancel 为 Try 占位，Compensate 为 Action 占位；通过插入影响行数识别空补偿、重复和悬挂；屏障与业务共用本地事务
- [x] 接收端数据库定位：TCC、Saga 使用接收服务配置的 DbContext；不把发起服务的 DbContext 类型、连接串哈希作为远端选库依据；MSG 原有定位机制保持兼容
- [x] 回调结果协议：Try、Action 的明确业务拒绝返回 HTTP 409；临时故障、Confirm、Cancel、Compensate 的失败保留可重试响应
- [x] 真实业务样例：订单、库存写入独立样例表，展示 TCC 资源预留、确认、释放，以及 Saga 正向和补偿状态；提供按业务编号查询状态的入口
- [x] 样例回调地址：明确直连 samplehost 与经 Gateway 的路径用法，避免直连地址重复附加服务名前缀
- [x] 回调参数处理：读取 chunked 请求体，操作参数与路径保持一致，缺省屏障编号确定为 `01`

## 复验清单

- [x] 构建修改后的 SampleHost，关闭自动打包，不发布 NuGet 包
- [x] 真实 HTTP TCC 成功：全局事务 succeed，订单确认，库存冻结清零且扣减正确
- [x] 真实 HTTP TCC 失败：全局事务 failed，订单取消，库存资源释放
- [x] 真实 HTTP Saga 成功：全局事务 succeed，订单、库存状态与正向操作一致
- [x] 真实 HTTP Saga 业务失败：返回确定失败，已成功的订单分支执行补偿
- [x] 完整 Host 容器可解析两个专用屏障接口，回调不再因空集合被拒绝
- [x] 真实 PostgreSQL 验证同操作去重、空补偿、迟到正向请求、业务异常回滚和并发锁竞争
- [x] 不同测试数据库的接收端使用各自本地 DbContext，不再因发起库哈希不同而拒绝
- [x] 真实 Saga 先扣库存、后续订单业务拒绝时，库存补偿恢复为可用 10、冻结 0、已扣 0
- [x] 真实 HTTP chunked 请求和重复 Try 只冻结一次，重复 Cancel 不重复释放
- [x] Prepare 后调用方取消，独立令牌仍成功 Abort，保留原取消异常
- [x] 二阶段业务异常返回 HTTP 500，屏障和业务事务回滚，后续重试能够真正执行
- [x] Submit 已被 DTM 接受但响应丢失时，保留传输异常，不发送 Abort
- [x] 测试事务达到终态，仅清理本次业务编号的数据和临时对象
- [x] 停止本次启动的宿主和辅助进程，确认监听端口释放，不停止用户已有服务

## 变更与验证记录

### 2026 年 11 月 10 日构建

构建修改后的 `samples/BeniceSoft.Abp.Sample.Host/BeniceSoft.Abp.Sample.Host.csproj`，关闭自动打包、共享编译及 MSBuild 节点复用，结果为 0 警告、0 错误。未新增测试项目或单元测试文件，未发布 NuGet 包。

### 完整 HTTP 链路

使用新构建的完整 SampleHost、配置中的真实测试 DTM 和 PostgreSQL 15.14。业务表在独立临时 schema 中手工初始化；完整宿主、回调中间件、屏障、领域服务和仓储均使用本次修改后的实现。

- TCC 成功：gid `codex-fix-tcc-ok-594915a090eb4271835a5b1d4a6c73b9`，全局 `succeed`，两个 Confirm 分支均 `succeed`，订单、库存均 `Confirmed`，库存为可用 9、冻结 0、已扣 1
- TCC 模拟失败：gid `codex-fix-tcc_failure-0b6a0f7cf9ce4e6693241338c2661911`，全局 `failed`，两个 Cancel 分支均 `succeed`，订单、库存均 `Cancelled`，库存恢复为 10、0、0
- Saga 成功：gid `codex-fix-saga_success-1110d3da632b4ea59fc6623c36a069d5`，全局 `succeed`，两个 Action 分支均 `succeed`，订单、库存均 `Applied`，库存为 9、0、1
- Saga 模拟失败：gid `codex-fix-saga_failure-a4c94a6023b34652904d93d25b4f09a4`，全局 `failed`，补偿分支均 `succeed`，订单 `Compensated`；库存 Action 未提交，空补偿没有创建库存行
- Saga 库存补偿：gid `codex-fix-saga-inventory-comp-2f66485ce3b047a0a4656522b006a3d6`，库存先实际扣减，后续订单业务拒绝；全局 `failed`，库存 `Compensated`，恢复为 10、0、0

故障场景的演示入口保留异常响应，验收断言采用 DTM 终态、补偿分支状态及实际数据库结果。宿主记录了 29 次本次回调，其中 2 次确定业务拒绝返回 409，1 次二阶段故障注入返回 500；回调 404、屏障接口缺失错误、分支方法未实现错误均为 0。

### 屏障和故障边界

- 真实 HTTP Cancel→Try→重复 Cancel 均成功应答，库存业务行始终不存在
- chunked 请求重复 Try 仅冻结 1 件，重复 Cancel 后仍为可用 10、冻结 0、已扣 0
- Confirm 在库存不存在时返回 500；随后 Try、Confirm 及重复 Confirm 正常完成，最终仅扣减 1 件，证明失败屏障没有被提前提交
- PostgreSQL 顺序、重复、异常回滚与重试结果均与 DTM SDK 一致
- PostgreSQL 并发中，Cancel 等待 Try 持有的原操作键，等待类型为 `Lock:transactionid`；Try 提交后执行真实补偿，Try 回滚后空补偿跳过业务
- 两个完整宿主容器连接 Default、TenantA 两个真实数据库，接收端在自身库写入 2 条屏障，来源库没有屏障表；错误来源类型、哈希也不改变本地选库；缺本地配置时明确失败
- Prepare 后取消用例 gid `codex-fixed-tcc-cancel-16bd15541a0d436586e7c7f2de772b0d`，Prepare、Abort 均返回 200，全局 `failed`，调用方收到原取消异常
- 提交响应丢失用例 gid `codex-fixed-submit-loss-no-logging-9795303506a0402e8c05dd31722255e5`，仅本机代理在真实 Submit 返回 200 后截断响应；调用方收到 `HttpRequestException`，没有发出 Abort，真实全局事务为 `succeed`。该独立验证容器禁用平台日志 provider，正式 SampleHost 的 Serilog 配置未修改

### 清理结果

完整宿主 PID 67084 已退出，6101 不再接受连接；提交故障注入代理的 16189 已关闭。宿主测试的临时 schema 已删除并确认剩余为 0，组件复验在两个测试库创建的独立 schema 和连接也全部清理。

本次创建的真实全局事务均已达到 `succeed` 或 `failed` 终态。DTM 事务历史及持久屏障记录保留，业务测试数据随独立临时 schema 删除。

### 使用边界

`IDtmTransFactory` 仅声明 `NewTcc(TransBase transaction)`。管理器创建并持有 TransBase，工厂用该对象创建 Tcc，分支调用和全局生命周期共享同一状态；不保留旧重载、默认接口实现或读取 SDK 内部状态的反射代码。

工厂接口简化后重新构建 SampleHost，结果为 0 警告、0 错误。完整宿主连接真实 DTM 和 PostgreSQL 复验：成功 gid `codex-simple-tcc-93ff0c9dbf8846bc8c5dcf0709069c0b` 为 `succeed`，订单和库存均 `Confirmed`，库存 9、0、1；模拟失败 gid `codex-simple-tcc-d00ec0e07a9148c2b0e3292a85233fe8` 为 `failed`，订单和库存均 `Cancelled`，库存恢复 10、0、0。本轮宿主 PID 41728 已退出，6101 已释放，独立临时 schema 剩余为 0。

手工构造 `DtmTccGlobalTransactionManager` 时需要提供新增的 `IDtmClient`、`ILogger` 依赖；常规 ABP 注入已通过完整宿主验证。

Saga 的修复范围是完善显式业务分支及补偿示例。发起方任意已提交数据仍需自行注册补偿逻辑，框架不自动撤销未知业务操作。

工作区原有的 `Directory.Packages.props`、`SampleDbContextFactory.cs` 变更不属于本次修复，不覆盖或还原。
