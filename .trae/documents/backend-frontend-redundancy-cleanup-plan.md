# 后端冗余清理与前端架构精简 — 缺口梳理与分批执行计划

## 一、总结

本计划梳理了项目中后端桥接层违规、API路由不一致、DI注册冗余、前端未使用代码等维度的细节缺口，按 P0/P1/P2 优先级分批执行。每批完成后确认再继续下一批。

**关键决策：**
- 按优先级分批（P0→P1→P2），不区分前后端
- 桥接层：可安全移除的直接移除；无原生替代的规划重构路径
- API路由：保留 `/api/runtime/` 前缀，仅统一无版本前缀的路由
- 前端：代码级清理（不调整包结构）

---

## 二、现状分析

### 2.1 后端桥接层（AGENTS.md 硬约束违规）

共发现 13 个桥接层文件，按可移除性分类：

| # | 文件路径 | 桥接模块 | 承载核心逻辑 | 有原生替代 | 可安全移除 |
|---|---------|---------|------------|----------|----------|
| 1 | `Atlas.AppHost/ExternalConnectors/Bridges/ConnectorLocalUserDirectoryBridge.cs` | ConnectorLocalUserDirectory ↔ LocalUserDirectory | 是 | 是 | ✅ |
| 2 | `Atlas.AppHost/ExternalConnectors/Bridges/ConnectorSecretProtectorBridge.cs` | ConnectorSecretProtector ↔ SecretProtector | 是 | 是 | ✅ |
| 3 | `Atlas.AppHost/ExternalConnectors/Bridges/ConnectorTenantContextWriterBridge.cs` | ConnectorTenantContextWriter ↔ TenantContextWriter | 是 | 是 | ✅ |
| 4 | `Atlas.Infrastructure/Services/Microflows/WorkflowRuntimeClientAdapter.cs` | WorkflowRuntimeClient ↔ RuntimeClient | 否 | 是 | ✅ |
| 5 | `Atlas.AppHost/Microflows/Infrastructure/MicroflowAuditWriterAdapter.cs` | MicroflowAuditWriter ↔ AuditWriter | 是 | 是 | ✅ |
| 6 | `Atlas.Infrastructure/Services/LowCode/AgentChannels/AgentChannelAdapters.cs` | AgentChannel ↔ RuntimeAgentChannel | 是 | 是 | ✅ |
| 7 | `Atlas.Infrastructure/Services/AiPlatform/CrossEncoderRerankerAdapter.cs` | CrossEncoderReranker ↔ Reranker | 否 | 是 | ✅ |
| 8 | `Atlas.Infrastructure/Services/Platform/AppBridgeServices.cs` | AppBridge ↔ AppBridgeService | 是 | 是 | ✅ |
| 9 | `Atlas.Infrastructure/Caching/HybridCacheSyncBridge.cs` | HybridCacheSync ↔ CacheSync | 否 | 是 | ✅ |
| 10 | `Atlas.Infrastructure/Services/AiPlatform/CozeNodeConfigAdapters.cs` | CozeNodeConfig ↔ NodeConfig | 是 | 是 | ✅ |
| 11 | `Atlas.Application.Microflows/Runtime/Actions/ConnectorRuntimeConnectorAdapters.cs` | ConnectorRuntime ↔ RuntimeConnector | 是 | **否** | ❌ 需重构 |
| 12 | `Atlas.Infrastructure/Events/Approval/ApprovalWorkflowBridgeEventHandler.cs` | ApprovalWorkflowBridge ↔ EventHandling | 是 | **否** | ❌ 需重构 |
| 13 | `Atlas.Infrastructure/Services/WorkflowEngine/WorkflowCanvasJsonBridge.cs` | WorkflowCanvasJson ↔ CanvasJson | 是 | **否** | ❌ 需重构 |

### 2.2 API 路由前缀不一致

| 前缀类别 | 数量 | 示例 | 问题 |
|---------|------|------|------|
| `/api/v1/` | ~60+ | `/api/v1/ai-assistants` | 主流标准 ✅ |
| `/api/v2/` | ~8 | `/api/v2/agentic-rag` | 有版本演进，可接受 |
| `/api/runtime/` | ~12 | `/api/runtime/chatflows` | 运行时专用，保留 |
| `/api/`（无版本） | 3 | `/api/app-web/workflow-sdk`, `/api/workflow_api`, `/api/intelligence_api` | **需统一** |
| `/api/app-web/` | 2 | `/api/app-web/coze-playground` | **需统一** |

### 2.3 DI 注册冗余

发现 20+ 个 ServiceCollection 扩展方法文件，分布在：
- `Atlas.Infrastructure/ServiceCollectionExtensions.cs`
- `Atlas.Infrastructure/PlatformServiceCollectionExtensions.cs`
- `Atlas.Infrastructure/AppRuntimeServiceCollectionExtensions.cs`
- `Atlas.Infrastructure/DependencyInjection/` 下 10+ 个注册文件
- `Atlas.Application/ServiceCollectionExtensions.cs`
- `Atlas.AppHost/Microflows/DependencyInjection/` 下 2 个文件
- `Atlas.AppHost/ExternalConnectors/AppHostExternalConnectorsExtensions.cs`
- 其他模块级注册文件

潜在问题：桥接层移除后，对应的 DI 注册也需同步清理。

### 2.4 后端项目引用

- `Atlas.Infrastructure` 引用了 `Atlas.Application`（可能违反分层原则：Infrastructure 不应依赖 Application）
- 未发现完全废弃的项目
- NuGet 包版本基本统一

### 2.5 前端冗余

| 类别 | 具体问题 | 文件/路径 |
|------|---------|----------|
| 未使用组件 | `global-create-modal.tsx` 定义但未被 import | `apps/app-web/src/app/components/` |
| 未使用组件 | `workspace-switcher.tsx` 定义但未被 import | `apps/app-web/src/app/components/` |
| Mock 数据 | `library-module-react/src/mock/` 下 3 个文件 | fixtures.ts, adapter.ts, index.ts |
| 临时样式 | `workflow-container/index.module.less` 标注临时方案 | `packages/workflow/playground/` |
| 临时逻辑 | `flowgram-canvas-interactions.ts` 标注 TODO 需重构 | `packages/mendix/mendix-microflow/` |
| HACK 标记 | `api-envelope.ts` 使用临时解决方案 | `packages/mendix/mendix-studio-core/` |
| deprecated | IDL 自动生成代码中多处 deprecated 标记 | `packages/arch/idl/src/auto-generated/` |

### 2.6 TODO/FIXME/HACK 标记

| 标记类型 | 位置 | 描述 |
|---------|------|------|
| TODO | `ConnectorTenantContextWriterBridge.cs` | 需实现更高效的上下文写入机制 |
| TODO | `ExternalIdentityBindingService.cs` | 待实现的外部身份绑定逻辑 |
| TODO | `flowgram-canvas-interactions.ts` | 临时逻辑需重构 |
| FIXME | `AppPublishArtifact.cs` | 需修复的问题 |
| HACK | `api-envelope.ts` | 临时解决方案绕过已知 bug |
| 临时方案 | `M20DataNodeExecutors.cs` | 多处临时处理逻辑 |
| 临时样式 | `workflow-container/index.module.less` | 临时 CSS 方案 |

---

## 三、分批执行计划

### P0 批：桥接层移除（AGENTS.md 硬约束）

**目标：** 移除 10 个可安全移除的桥接层，规划 3 个需重构的桥接层路径。

#### P0-1：ExternalConnectors Bridges 移除（3 个文件）

**文件：**
1. `src/backend/Atlas.AppHost/ExternalConnectors/Bridges/ConnectorLocalUserDirectoryBridge.cs`
2. `src/backend/Atlas.AppHost/ExternalConnectors/Bridges/ConnectorSecretProtectorBridge.cs`
3. `src/backend/Atlas.AppHost/ExternalConnectors/Bridges/ConnectorTenantContextWriterBridge.cs`

**操作步骤：**
1. 读取每个桥接类，确认其委托的目标接口和实际实现类
2. 找到所有引用这些桥接类的调用点（构造函数注入、DI 注册）
3. 将调用点改为直接使用原生实现
4. 删除桥接类文件
5. 清理 `AppHostExternalConnectorsExtensions.cs` 中对应的 DI 注册
6. `dotnet build` 验证

#### P0-2：Infrastructure 层 Bridge/Adapter 移除（5 个文件）

**文件：**
1. `src/backend/Atlas.Infrastructure/Services/Microflows/WorkflowRuntimeClientAdapter.cs`
2. `src/backend/Atlas.Infrastructure/Services/LowCode/AgentChannels/AgentChannelAdapters.cs`
3. `src/backend/Atlas.Infrastructure/Services/AiPlatform/CrossEncoderRerankerAdapter.cs`
4. `src/backend/Atlas.Infrastructure/Services/Platform/AppBridgeServices.cs`
5. `src/backend/Atlas.Infrastructure/Caching/HybridCacheSyncBridge.cs`

**操作步骤：**
1. 逐一读取桥接类，确认委托关系
2. 找到所有调用点和 DI 注册
3. 替换为原生实现引用
4. 删除桥接类文件
5. 清理对应 DI 注册
6. `dotnet build` 验证

#### P0-3：Microflow/AI 层 Adapter 移除（2 个文件）

**文件：**
1. `src/backend/Atlas.AppHost/Microflows/Infrastructure/MicroflowAuditWriterAdapter.cs`
2. `src/backend/Atlas.Infrastructure/Services/AiPlatform/CozeNodeConfigAdapters.cs`

**操作步骤：** 同 P0-2

#### P0-4：无原生替代桥接层的重构路径规划（3 个文件，仅规划不执行）

**文件：**
1. `ConnectorRuntimeConnectorAdapters.cs` — 需在 `Atlas.Application.Microflows` 或 `Atlas.Infrastructure` 中补齐原生连接器运行时适配实现
2. `ApprovalWorkflowBridgeEventHandler.cs` — 需在 `Atlas.Infrastructure/Events/` 中补齐原生审批工作流事件处理
3. `WorkflowCanvasJsonBridge.cs` — 需在 `Atlas.Infrastructure/Services/WorkflowEngine/` 中补齐原生画布 JSON 转换

**每个文件的重构路径：**
- 步骤 1：读取桥接类完整代码，提取其承载的核心业务逻辑
- 步骤 2：在正确的架构分层中创建原生实现类（Service/EventHandler/Converter）
- 步骤 3：更新 DI 注册指向原生实现
- 步骤 4：更新所有调用点
- 步骤 5：删除桥接类
- 步骤 6：补齐单元测试

**注意：** P0-4 仅产出重构路径文档，不在本批次执行代码变更。

---

### P1 批：API 路由统一 + DI 注册清理

#### P1-1：无版本前缀 API 路由统一

**需处理的路由：**
| 当前路由 | 目标路由 | Controller |
|---------|---------|-----------|
| `/api/app-web/workflow-sdk` | `/api/v1/workflow-sdk` | `AppWebWorkflowGatewayController` |
| `/api/workflow_api` | `/api/v1/workflow-api` | `AppWebWorkflowGatewayController` |
| `/api/intelligence_api` | `/api/v1/intelligence` | `CozeIntelligenceCompatController` |
| `/api/app-web/coze-playground` | `/api/v1/coze-playground` | `AppWebCozePlaygroundGatewayController` |
| `/api/app-web/coze-developer` | `/api/v1/coze-developer` | `AppWebCozeDeveloperGatewayController` |

**操作步骤：**
1. 修改 Controller 的 `[Route]` 特性
2. 搜索前端代码中所有对应的 API 调用路径，同步更新
3. 更新 `.http` 测试文件和 mock 数据
4. 更新 `contracts.md` 中的路由记录
5. `dotnet build` + 前端 `pnpm run build:app-web` 验证

#### P1-2：桥接层移除后的 DI 注册清理

**涉及文件：**
- `Atlas.AppHost/ExternalConnectors/AppHostExternalConnectorsExtensions.cs`
- `Atlas.Infrastructure/DependencyInjection/` 下相关注册文件
- `Atlas.AppHost/Microflows/DependencyInjection/MicroflowBackendServiceCollectionExtensions.cs`
- `Atlas.AppHost/Program.cs`（如有直接注册）

**操作步骤：**
1. 逐一检查 P0 批移除的桥接类对应的 DI 注册是否已清理
2. 搜索是否有遗漏的 `services.AddScoped/AddTransient/AddSingleton` 注册指向已删除的桥接类
3. 清理所有残留注册
4. `dotnet build` 验证

#### P1-3：Infrastructure → Application 反向依赖标记

**问题：** `Atlas.Infrastructure` 引用了 `Atlas.Application`，违反标准分层（Infrastructure 不应依赖 Application）。

**操作：** 本次仅标记和记录，不执行重构（该问题影响面大，需单独排期）。

---

### P2 批：前端代码级清理 + 后端临时标记清理

#### P2-1：前端未使用组件清理

**文件：**
1. `src/frontend/apps/app-web/src/app/components/global-create-modal.tsx`
2. `src/frontend/apps/app-web/src/app/components/workspace-switcher.tsx`

**操作步骤：**
1. 全局搜索确认这两个组件确实未被任何文件 import
2. 删除文件
3. `pnpm run build:app-web` 验证

#### P2-2：前端 Mock 数据清理

**文件：**
1. `src/frontend/packages/library-module-react/src/mock/fixtures.ts`
2. `src/frontend/packages/library-module-react/src/mock/adapter.ts`
3. `src/frontend/packages/library-module-react/src/mock/index.ts`

**操作步骤：**
1. 确认这些 mock 文件是否仅在开发模式使用
2. 如果有生产环境引用，需替换为真实 API 调用
3. 如果仅开发用，保留但标记 `// @internal dev-only`
4. `pnpm run build:app-web` 验证

#### P2-3：前端临时标记清理

**文件：**
1. `packages/mendix/mendix-microflow/src/flowgram/flowgram-canvas-interactions.ts` — TODO 重构
2. `packages/mendix/mendix-studio-core/src/microflow/contracts/api/api-envelope.ts` — HACK 临时方案
3. `packages/workflow/playground/src/components/workflow-container/index.module.less` — 临时样式

**操作：** 评估每个标记的实际影响，能修复的修复，不能修复的补充详细注释说明原因和计划。

#### P2-4：后端临时标记清理

**文件：**
1. `ConnectorTenantContextWriterBridge.cs` — TODO（P0 移除后自动解决）
2. `ExternalIdentityBindingService.cs` — TODO 待实现
3. `AppPublishArtifact.cs` — FIXME
4. `M20DataNodeExecutors.cs` — 多处临时处理

**操作：** 评估每个标记，能修复的修复，不能修复的补充详细注释。

#### P2-5：前端 IDL deprecated 标记清理

**路径：** `src/frontend/packages/arch/idl/src/auto-generated/`

**操作：** 自动生成代码中的 deprecated 标记，需确认 IDL 源定义是否已更新。如果是上游已废弃的接口，需在前端调用点替换为新接口。本次仅标记，不修改自动生成代码。

---

## 四、假设与决策

| # | 假设/决策 | 依据 |
|---|---------|------|
| 1 | 桥接类"有原生替代"的判断基于代码探索结果 | 搜索代理分析 |
| 2 | `/api/runtime/` 前缀保留不动 | 用户明确选择 |
| 3 | 前端 adapter 包（space-store-adapter 等）不违规，保留 | 分析确认不承载核心业务逻辑 |
| 4 | Infrastructure → Application 反向依赖仅标记 | 影响面大，需单独排期 |
| 5 | IDL 自动生成代码不手动修改 | 需从上游 IDL 定义更新 |
| 6 | 每批执行前需确认，执行后需 `dotnet build` + 前端 build 验证 | AGENTS.md 闭环规则 |

---

## 五、验证步骤

每批执行后必须完成：

1. **后端：** `dotnet build` 通过（最小影响范围）
2. **前端：** `pnpm run build:app-web` 通过
3. **定向测试：** 对修改的模块运行 `dotnet test`
4. **DI 完整性：** 启动后端确认无 DI 解析异常
5. **API 实测：** 对路由变更的端点做 API 调用验证

---

## 六、风险与下一步

| 风险 | 缓解措施 |
|------|---------|
| 桥接层调用点遗漏导致编译失败 | 全局搜索引用 + build 验证 |
| DI 注册遗漏导致运行时异常 | 启动后端确认无解析异常 |
| API 路由变更导致前端调用失败 | 同步更新前端 API 路径 + build 验证 |
| 无原生替代的桥接层重构引入回归 | P0-4 仅规划不执行，需单独排期 |
| Infrastructure→Application 依赖重构影响面大 | 仅标记，后续单独任务处理 |

**下一步：** 用户确认计划后，从 P0-1 开始执行。
