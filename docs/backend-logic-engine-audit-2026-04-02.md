# 后台增强引擎 190 Case 复核清单（2026-04-02）

## 复核口径

- 判定标准：代码实现 + `contracts.md` 同步 + `.http`/前端页面齐备。
- 范围：`docs/plan-backend-logic-engine-progress.md` 中 Track-01/02/03/04/05/06/08/09/10，共 190 Case。

## 统计结果（当前基线）

| Track | 已实现 | 部分实现 | 未实现 |
|------|------:|------:|------:|
| Track-01 | 7 | 0 | 0 |
| Track-02 | 27 | 9 | 0 |
| Track-03 | 17 | 7 | 0 |
| Track-04 | 12 | 6 | 0 |
| Track-05 | 14 | 9 | 7 |
| Track-06 | 10 | 12 | 0 |
| Track-08 | 0 | 6 | 5 |
| Track-09 | 5 | 17 | 4 |
| Track-10 | 6 | 10 | 0 |
| **合计** | **98** | **76** | **16** |

> 说明：`docs/plan-backend-logic-engine-progress.md` 顶部汇总当前写为 `96/79/15`，与上表（按 Track 小计）不一致；后续需统一为单一数字来源。

## 本轮已落地的关键整改

- `docs/contracts.md` 已补齐正式章节：`逻辑流与执行`、`治理与插件`。
- `DatabaseInitializerHostedService` 已纳入 LogicFlow/BatchProcess 关键实体建表初始化。
- `Atlas.Infrastructure.BatchProcess` 已注册 `IWorkerPool`。
- `LogicFlowDesignerPage.vue` 已挂载 designer 子组件（工具栏/画布/结构树/diff/调试/属性/节点/对象）。
- `extra-messages.ts` 已补 `route.batchDesigner`、`route.batchMonitor`、`route.batchDeadLetters` 中英文词条。

## Top 缺口 Case（优先修复）

1. `T05-19-B` 错误分支路由闭环
2. `T05-20-B` 补偿图执行器
3. `T05-22-B` 子流程执行器
4. `T05-23-B` 条件执行器
5. `T05-24-B` 循环执行器
6. `T05-28-F` X6 画布基础骨架
7. `T05-29-F` 拖拽与连线能力
8. `T08-02-B` 节点级幂等
9. `T08-03-B` 批次幂等键
10. `T08-04-B` 错误分类枚举与策略路由

## 证据文件

- `docs/contracts.md`
- `src/backend/Atlas.Infrastructure/Services/DatabaseInitializerHostedService.cs`
- `src/backend/Atlas.Infrastructure.BatchProcess/ServiceCollectionExtensions.cs`
- `src/backend/Atlas.Infrastructure.LogicFlow/Services/FlowExecutionCommandService.cs`
- `src/frontend/Atlas.WebApp/src/pages/logic-flow/LogicFlowDesignerPage.vue`
- `src/frontend/Atlas.WebApp/src/i18n/extra-messages.ts`
