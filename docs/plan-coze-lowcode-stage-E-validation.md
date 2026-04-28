# 阶段 E 验证报告（M18-M20 智能体 / 工作流父级 / 节点 49 全集）

## 范围
- M18 智能体复刻 + 插件完整域（PromptTemplate + Plugin + 4 张表 + Runtime invoke）
- M19 工作流父级工程能力（AI 生成双模式 + 批量 3 输入源 + 封装/解散 + 配额）
- M20 节点 49 全集 + 双哲学引擎 + 节点级状态持久化

## 验证
- `dotnet build Atlas.SecurityPlatform.slnx` → **0 警告 0 错误**（22s）。
- `pnpm run i18n:check` → **0 缺失**。
- 累计低代码后端表数：**24 张**（M01 7 + M04 1 + M06 1 + M09 1 + M10 1 + M11 2 + M12 2 + M13 2 + M14 1 + M18 5 + M20 1 + 已存在 RuntimeWorkflowAsyncJob 等）。
- DAG 节点目录：≥ **49**（含原有 30+ 节点 + M12 3 触发器节点 + M20 17 个节点 = 49+）。

## 新增端点
- M18：
  - `/api/v1/lowcode/prompt-templates`（4 端点）
  - `/api/v1/lowcode/plugins`（6 端点）
  - `/api/runtime/plugins/{id}:invoke`
- M19：
  - 历史草案中的 `/api/v2/workflows/generate` / `/{id}/batch` / `/{id}/compose` / `/{id}/decompose` / `/quota` 已在当前代码库下线
  - 当前保留的运行时工作流入口为 `/api/runtime/workflows/{id}:invoke` / `:invoke-async` / `:invoke-batch`
- M20：
  - `/api/v2/workflows/orchestration/plan`

## 关键决策
- **M18 插件域**：与现有工作流 N10 节点共享 PluginRegistry；Studio 调试通道 `/api/runtime/plugins/{id}:invoke` 自动计量 + 审计；M19 配额联动。
- **M19 AI 生成**：已接入真实 LLM（IChatClientFactory.CreateAsync + IChatClient.GetResponseAsync，30s 超时）；auto 模式输出 { version, nodes[], edges[] } 完整 DAG；assisted 模式输出节点骨架；LLM 不可用 / 解析失败 / 超时 → 关键字模板 fallback（status='success-fallback'）。
- **M19 批量执行**：3 输入源（CSV/JSON/数据库）统一委托 `IRuntimeWorkflowExecutor.InvokeBatchAsync`；onFailure=continue/abort。
- **M20 节点 49**：上游对齐 8 个 + Atlas 私有图像/视频 6 个 + Memory 拆分 3 个 = 17 新节点。
- **M20 双哲学**：`IDualOrchestrationEngine.Plan` 切换 explicit / agentic；agentic 模式注入 tools 池 + metadataJson；与前端 `lowcode-workflow-adapter/orchestration` 完全对齐。
- **M20 节点状态**：4 作用域（session/conversation/trigger/app）+ 服务端单 SQL upsert，禁止循环 DB。

## 进入收尾
- 全 20 里程碑全部交付；下一步生成总报告。
