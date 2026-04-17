# 低代码运行时协议规格（lowcode-runtime-spec）

> 状态：M00 预创建 stub。
> 范围：M01 Schema 字段全集 + M02 表达式 7 作用域与隔离规则 + M03 动作链与状态补丁 + M08 RuntimeSchema + M09 Workflows invoke / async / batch + M10 Files prepare-upload / complete-upload + M11 Chatflow SSE 协议 + 中断/恢复/插入 + M12 Triggers / Webview Domains + M13 dispatch 协议 / Trace 6 维检索 + M14 Versions 双套端点 + M17 Publish 运行时只读端点。
>
> 端点前缀双套：设计态 `/api/v1/lowcode/*`（PlatformHost 5001） / 运行时 `/api/runtime/*`（AppHost 5002），禁止混用。

## 章节占位

- §1 Schema 字段全集（M01）
- §2 表达式语法、7 作用域、隔离规则（M02）
- §3 动作链与状态补丁、超时熔断降级（M03）
- §4 RuntimeSchemaController（M08）
- §5 RuntimeWorkflowsController（同步 / 异步 / 批量）（M09）
- §6 RuntimeFilesController（M10）
- §7 RuntimeChatflowsController + Sessions + 中断 / 恢复 / 插入（M11）
- §8 RuntimeTriggersController + RuntimeWebviewDomainsController（M12）
- §9 RuntimeEventsController.Dispatch + Trace 6 维检索（M13）
- §10 RuntimeVersionsController（archive / rollback）+ AppVersionsController（设计态 v1）（M14）
- §11 Runtime publish artifacts 只读端点（M17）

> 各章节由对应里程碑落地。提前实现属阶段越界，禁止合入。
