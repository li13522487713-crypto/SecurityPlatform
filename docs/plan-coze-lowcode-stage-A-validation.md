# 阶段 A 验证报告（M01-M03 协议层）

## 范围
- M00：23 packages + 4 apps + 后端 Atlas.Domain.LowCode/Application.LowCode + 12 契约文档骨架。
- M01：lowcode-schema 17 类完整类型 + zod + guards + migrate + shared；后端 7 实体 + Repo + Service + LowCodeAppDefinitionsController + .http + Schema catalog。
- M02：lowcode-expression jsonata + 模板 + 7 作用域 + 隔离 + 推断 + 依赖追踪 + Monaco LSP 适配器。
- M03：lowcode-action-runtime 7 内置动作 + 编排 + 状态补丁 + loading/error + scope-guard + resilience + extend。

## 验证结果

### 后端
- `dotnet build Atlas.SecurityPlatform.slnx` → **0 警告 0 错误**（22.13s）。

### 前端
- `pnpm run i18n:check` → **0 缺失**（zh/en 对齐）。
- `pnpm test` 各包：
  - `@atlas/lowcode-schema` → **22 通过**（zod 5 binding × 2 + 6 contentParam + 7 action + Event/Component 递归 + Page/App + 9 valueType + Patch/Trace + 8 guards + 3 migrate）。
  - `@atlas/lowcode-expression` → **30 通过**（jsonata 3 + template 6 + scope 4 含 30 跨作用域违规批量 + deps 5 + inference 4 + monaco 6）。
  - `@atlas/lowcode-action-runtime` → **23 通过**（state-patch 5 + resilience 6 + dispatcher 8 + chain 4）。
- 合计 **75 测试通过**（覆盖 5 binding × 2 / 6 contentParam / 7 action / 7 作用域 / 30 跨作用域违规 / 7 内置动作 / 4 链式编排 / 弹性 6 场景 等核心闭环）。

## 关键决策与对齐
- API 双前缀强约束：设计态 `/api/v1/lowcode/apps`（PlatformHost 5001）已落地 11 个端点；运行时 `/api/runtime/*` 留待 M08+。
- 标准化协议唯一桥梁：M03 dispatcher 内部对 call_workflow / call_chatflow 走 `ctx.invokeDispatch`，禁止内部直 fetch；为 M13 dispatch 后端落地保留接口。
- 作用域隔离：M02 + M03 双层 scope-guard，writable=page/app，其它 5 类只读。
- 元数据驱动：M06 校验器尚未落地，但 ComponentMeta 类型已含 bindableProps / contentParams / supportedEvents / childPolicy 完整字段。
- 资源治理：M01 已建 AppResourceReference 反查表，M14 完整使用。
- 等保 2.0：所有写接口已经 IAuditWriter；JSON 列存留待 M14 加密扩展。

## 进入阶段 B
- M04 lowcode-editor-canvas（dnd-kit + 三布局 + 快捷键 ≥40）开始。
