# Coze 低代码差距补齐 — P0 紧急止血验证报告

> 范围：PLAN §P0-1 ~ §P0-6（契约断裂 / 运行期失败 / 等保合规）
> 完工时间：2026-04-18

## 1. 修复项总览

| 编号 | 描述 | 修改文件 | 关联 PLAN |
| --- | --- | --- | --- |
| P0-1 | M11 sessions :switch 路由补齐 | `IRuntimeSessionAndChatflowServices.cs` / `RuntimeSessionAndChatflowServices.cs` / `RuntimeChatflowsController.cs` / `RuntimeChatflows.http` | M11 C11-6 + S11-2 |
| P0-2 | M12 三个 Trigger 节点 Executor 实现并注册 | `TriggerNodeExecutors.cs` (新) / `NodeExecutorRegistry.cs` | M12 S12-3 |
| P0-3 | M20 17 个节点 Executor 实现并注册 + DagExecutor 抛错 | `M20DataNodeExecutors.cs` (新) / `M20MediaNodeExecutors.cs` (新) / `NodeExecutorRegistry.cs` / `DagExecutor.cs` | M20 S20-1 + S20-2 |
| P0-4 | M12 DNS TXT 真实实现 | `Atlas.Infrastructure.csproj` / `RuntimeTriggerAndWebviewServices.cs` | M12 C12-2 + S12-2 |
| P0-5 | M12 open_external_link 服务端白名单 | `ITriggerAndWebviewServices.cs` / `RuntimeTriggerAndWebviewServices.cs` / `RuntimeTraceAndDispatchServices.cs` | M12 C12-5 + 等保 2.0 §1.3.6 |
| P0-6 | M16 Collab Hub app 级权限 | `LowCodeCollabHub.cs` | M16 S16-1 |

## 2. 关键技术细节

### P0-1 sessions :switch
- 新增 `IRuntimeSessionService.SwitchAsync(...)`：404 / 403 / 200 三种路径明确
- 路由 `POST /api/runtime/sessions/{id}:switch`
- 服务端不持久化"当前活跃 sessionId"，前端将返回的 sessionId 作为后续请求 SessionId 参数（避免引入冗余状态）
- 跨用户访问：`OwnerUser != currentUser` → 拒绝并审计 `lowcode.runtime.session.switch failed reason:forbidden`

### P0-2 + P0-3 节点 Executor
- 共 20 个新 Executor（M12 3 + M20 17）
- **DagExecutor.ExecuteNodeAsync 修正**：`executor is null` 时不再返回 `SuccessResult` 静默吞业务，改为：
  - 仅 `Comment` 节点保留跳过（无运行时语义）
  - 其余抛 `NODE_EXECUTOR_NOT_REGISTERED` 错误信息，让 DagExecutor 走标准失败链路 `PersistBlockedByFailureAsync`
- 媒体节点（图像/视频生成）通过 `MediaProviderGuard.EnsureProviderConfigured` 守门：未配置 `IChatClientFactory` → `BusinessException("MODEL_PROVIDER_NOT_CONFIGURED")`，与 PLAN §七风险 1 一致

### P0-4 DNS TXT 真实实现
- 引入 `DnsClient 1.8.0` NuGet 包
- 子域名约定：`_atlas-webview-verify.{domain}` 的 TXT 记录任一值等于 `entity.VerificationToken` 即通过
- 错误细分：`dns_txt:dns-error` / `dns_txt:no-record` / `dns_txt:token-mismatch` / `dns_txt:timeout` / `dns_txt:dns-exception:{Code}` / `dns_txt:exception:{Type}`
- 不再"未实现也通过"

### P0-5 服务端白名单守门
- `IRuntimeWebviewDomainService.IsAllowedAsync(tenantId, url, ct)` 实现：
  - URL 合法性 + 仅 http(s)（拒 javascript/data/file/ftp）
  - host 大小写不敏感 + 精确匹配 + 子域名匹配（`a.example.com` 命中 `example.com`）
  - 防 evilfoo.example.com / fooexample.com 类绕过（必须 `endsWith("." + allowed)`）
  - 未 verified 的域名一律拒绝
- DispatchExecutor `open_external_link` 分支：拒绝时写错误码 `WEBVIEW_DOMAIN_NOT_ALLOWED` + 失败审计

### P0-6 Collab Hub app 级权限
- `LowCodeCollabHub.JoinApp` / `SendUpdate` 都通过 `IAppDefinitionRepository.FindByCodeAsync` / `FindByIdAsync` 校验 appId 必须在当前租户存在
- 不存在 → `HubException("APP_ACCESS_DENIED: ...")`
- 防止同租户用户越权 JoinApp("any-app-id")

## 3. 验证

### 后端构建（0 警告 0 错误）
```
dotnet build Atlas.SecurityPlatform.slnx
已成功生成。
    0 个警告
    0 个错误
已用时间 00:00:14.58
```

附带修复 Atlas.SecurityPlatform.slnx：移除 `Atlas.Application.LowCode` 与 `Atlas.Domain.LowCode` 的 `<Build Solution="Debug|*" Project="false" />` 标签（FINAL 报告遗留的 build 跳过配置）。

### 单元测试（371 个，全绿）
```
dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName!~Integration"
已通过! - 失败:     0，通过:   371，已跳过:     0，总计:   371
```

新增守门单测 34 个：
- `NodeExecutorRegistryCoverageTests`（22 个）：每一种 WorkflowNodeType 必须有 Executor 注册，明确列出 P0-2 + P0-3 全部 20 个新节点
- `RuntimeWebviewDomainAllowListTests`（8 个）：白名单 URL 匹配语义全覆盖
- `RuntimeSessionSwitchTests`（4 个）：404 / 403 / 成功 / 归档场景

### 工作流测试
```
dotnet test tests/Atlas.WorkflowCore.Tests
已通过! - 通过: 4
```

### 前端 i18n
```
[i18n-audit] target=app-web
[i18n-audit] used keys=546
[i18n-audit] zh missing=0, unresolved=0, autofill=0
[i18n-audit] en missing=0, unresolved=0, autofill=0
```

## 4. 已知简化与延后项

- **DNS TXT 真实查询**：使用系统默认 DNS，未配置自定义 NameServer；多机房 / 私有解析场景按需扩展 `LookupClientOptions.NameServers`。
- **Trigger Node currentUserId**：工作流执行上下文当前无用户身份贯穿（全局 `_currentUser` 不能在 Hangfire/system 调用使用），传 `0L` 占位；后续若引入 NodeExecutionContext.UserId 可移除。
- **媒体节点**（ImageGeneration / VideoGeneration 等）输出仅是"指令型 outputs"，由前端或下游 Adapter 接入真实模型供应商；未配置时报 `MODEL_PROVIDER_NOT_CONFIGURED`，与 FINAL "模型供应商真实接入" 延后项一致。
- **LowCodeCollabHub 权限**：当前以"租户内 app 存在"为权限边界；后续可在 `IAppDefinitionRepository` 上扩展 `CanEditAsync(userId, appId)` 替换。

## 5. 进入 P1

P0 全部 6 项闭环。下一步进入 P1 核心 UI 填实（47 个 Web 组件 + 设计器三件套 + 画布集成 + AiChat + Yjs 协同绑定）。
