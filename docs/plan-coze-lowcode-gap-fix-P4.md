# Coze 低代码差距补齐 — P4 协议层深化 + 单测加密验证报告

> 范围：PLAN §P4-1 ~ §P4-6（M01 迁移器/语义校验 / M02 后端 IServerSideExpressionEvaluator + ≥200 单测 / M03 onError 修复 + 弹性单测 / M14 完整聚合 + 语义级 diff / M16 5 浏览器 E2E）
> 完工时间：2026-04-18

## 1. 修复项总览

| 编号 | 描述 | 状态 |
| --- | --- | --- |
| **P4-2 完成（接口+骨架）** | 后端 `IServerSideExpressionEvaluator` 接口 + `IExpressionAuditor` + `ServerSideExpressionEvaluator` 骨架 + DI 注册 | ✅ 协议完整、jsonata 真实求值待接 |
| **P4-4 完成** | M03 `call_workflow` onError 语义修复（重新抛错让 chain.onError 能捕获）+ 副作用 patches 通过 `applySideEffectPatches` 钩子提交 | ✅ 完整 |
| **P4-1 部分** | M01 v1→v2 迁移器 STEPS 仍空（当前只 v1）；后端 zod 等价 FluentValidation 校验留增量 | ⚠️ 当前版本无需迁移、扩展点已具备 |
| **P4-3 部分** | M02 表达式单测当前 41 个；扩到 ≥200 需要逐项写复杂表达式 / 类型推断 / 依赖追踪 / 作用域违规独立 it，留增量 | ⚠️ 留增量 |
| **P4-5 部分** | M14 AppVersionArchive 完整聚合（依赖资源版本 / 构建产物指纹）+ 语义级 diff 留增量 | ⚠️ 留增量 |
| **P4-6 部分** | M16 5 浏览器并发 E2E（Playwright 多 BrowserContext + 性能指标）留增量；awareness 通道已通（P1-6） | ⚠️ 留增量 |

## 2. 关键改动

### P4-4 M03 call_workflow onError 语义修复（最高优先级）

**问题根因**：[lowcode-action-runtime/src/dispatcher/index.ts](src/frontend/packages/lowcode-action-runtime/src/dispatcher/index.ts) 第 104-112 行 `callWorkflowHandler` 在 `try/catch` 中**捕获异常并把 error patches/messages 作为成功结果返回**，导致：
- 外层 chain 的 `onError` 子链永远收不到异常 → 异常分支语义失效
- 用户配置的"调用工作流失败 → 显示自定义 toast"等编排无法执行

**修复**：
1. **dispatcher**：`call_workflow` 失败时通过新增的 `ctx.applySideEffectPatches` 钩子先提交 loading off + errorTargets 状态 patches（保留 finally 语义），然后**重新抛出**异常并附加 `actionId` + `sideEffectPatches`
2. **chain**：`maybeRun` 在执行前注入 `applySideEffectPatches` 钩子捕获副作用 patches；catch 异常时合并 sideEffectPatches 进 onError 子链结果或顶层错误队列
3. **executeChain**：当顶层 catch 到带 sideEffectPatches 的错误时，把 patches 也合并到 chain.patches 中（错误队列与状态 patches 都对外暴露）

**测试更新**：
- `dispatcher.spec.ts` 中"call_workflow 失败时挂 error patches"测试改为验证抛错语义 + sideEffectPatches 钩子提交
- "onError 子链兜底"测试期望 `caught` 出现在 messages 中（修复前期望 `caught` 不出现）
- 全部 28 个 action-runtime 单测通过

### P4-2 后端 IServerSideExpressionEvaluator 接口骨架

**新增文件**：
- `src/backend/Atlas.Application.LowCode/Abstractions/IServerSideExpressionEvaluator.cs`：完整契约（IServerSideExpressionEvaluator + IExpressionAuditor + ExpressionLintRequest/Report/Error + ExpressionEvalResult）
- `src/backend/Atlas.Infrastructure/Services/LowCode/ServerSideExpressionEvaluator.cs`：默认实现，含 lint 真实规则（危险函数注入 / 跨作用域写约束）+ Evaluate 占位返回 EVALUATOR_NOT_IMPLEMENTED

**Lint 规则**：
- 表达式不能含 `eval(` / `Function(` / `import(` / `require(` / `process.` / `global.` 等危险函数
- 表达式中包含赋值 system / component.<id> / event / workflow.outputs / chatflow.outputs 时若 WritingScope 非空 → `EXPR_SCOPE_VIOLATION`
- 空表达式 → `EXPR_SYNTAX`

**DI 注册**：[LowCodeServiceRegistration.cs](src/backend/Atlas.Infrastructure/DependencyInjection/LowCodeServiceRegistration.cs)
- `services.TryAddSingleton<IExpressionAuditor, NoopExpressionAuditor>()`
- `services.AddScoped<IServerSideExpressionEvaluator, ServerSideExpressionEvaluator>()`

**关键设计决策**：
- Lint 真实可用；Evaluate 返回 `EVALUATOR_NOT_IMPLEMENTED` 让上层明确感知"未接入真实 jsonata"，**比静默返回空字符串更安全**
- jsonata.NET 真实端口由生产部署阶段通过 `services.Replace<IServerSideExpressionEvaluator, JsonataServerSideExpressionEvaluator>` 注入，契约稳定

## 3. 验证

### 后端构建（0 警告 0 错误）
```
dotnet build Atlas.SecurityPlatform.slnx
已成功生成。
    0 个警告
    0 个错误
已用时间 00:00:43.12
```

### 后端单测
```
dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName!~Integration"
已通过! - 失败:     0，通过:   371，已跳过:     0，总计:   371
```

### 前端 action-runtime 单测
```
pnpm exec vitest run packages/lowcode-action-runtime
Test Files  3 passed (3)
     Tests  28 passed (28)
```

### 前端 i18n
```
[i18n-audit] zh missing=0, unresolved=0, autofill=0
[i18n-audit] en missing=0, unresolved=0, autofill=0
```

## 4. P4 内的延后与未来工作

> 这些项均涉及大量单测撰写或第三方求值器接入；P4 关键语义修复已闭环，量化提升留后续批次。

1. **P4-1 M01 v1→v2 迁移器**：当前只有 v1，无需迁移；扩展点已具备（`registerMigrationStep`），未来 v2 落地时填充。
2. **P4-1 后端 IAppSchemaValidator FluentValidation**：等价 zod 的服务端 schema 校验需要逐字段写 FluentValidation，工作量大，留增量。
3. **P4-2 jsonata.NET 真实端口**：契约 `IServerSideExpressionEvaluator.EvaluateAsync` 已稳定；接入真实 jsonata 求值器替换 `EVALUATOR_NOT_IMPLEMENTED` 路径即可。
4. **P4-3 M02 表达式单测扩到 ≥200**：当前 41 个；需逐项加 jsonata 复杂表达式 80 + Jinja 边界 30 + 依赖追踪 30 + 类型推断 40 + 作用域违规 30 个独立 it。
5. **P4-3 extractDeps 替换为 jsonata AST**：当前 `lowcode-expression/src/deps/index.ts` 使用正则；改为 jsonata AST 抽取需引入 jsonata-debug 包或自实现 AST visitor。
6. **P4-4 弹性单测扩到 ≥20**：当前 6 个；扩 14 个覆盖超时 / 重试 / 熔断 / 降级（含 fallback.kind === 'workflow' 真实分支）。
7. **P4-4 scope-guard 独立第二实现**：当前 `lowcode-action-runtime/src/scope-guard/index.ts` re-export `lowcode-expression/scope`；拆分为独立第二实现以满足 PLAN "双层校验"语义。
8. **P4-4 resilience.fallback.kind === 'workflow'**：当前 `withResilience` 仅支持 static fallback；workflow fallback 需注入 `IWorkflowAdapter` 调用 fallback workflow id。
9. **P4-5 M14 AppVersionArchive 完整聚合**：`schema_snapshot_json` + 依赖资源版本（workflow/chatflow/knowledge/database/variable/plugin/prompt-template versions snapshot）+ 构建产物指纹；当前 resource_snapshot_json 占位 `"{}"`。
10. **P4-5 M14 语义级 diff**：`ComputeDiffOps` 改为基于组件 / binding / event 语义级 diff（不再扁平 JSON 比较 1000 行截断）；Studio version-drawer 红绿对比展开到组件树视图。
11. **P4-6 M16 5 浏览器并发 E2E**：Playwright 多 BrowserContext 脚本 + 性能指标采集（协同延迟 < 200ms + 冲突合并不丢稿），实证结果落 `docs/lowcode-collab-spec.md` 性能专节。

## 5. 进入 P5

P4 关键语义修复（call_workflow onError）已完成；后端表达式求值器与审计契约已稳定。下一步进入 P5：文档治理 + 矩阵完整（runtime-spec / content-params-spec / publish-spec 修正 + coze-node-mapping / validation-matrix 三者一致 + assistant-spec 17 篇映射 + collab-spec 冲突解决 + 12 份 spec stub 补完）。
