# Microflow 内联主线验收审计（2026-05-05）

## 目标重述

- 以 FlowGram 节点/连线为主入口完成阅读、配置、调试。
- 右侧属性面板与底部 Debug Panel 默认不作为主路径。
- 保持保存、撤销、重做、运行、trace 投影能力不破坏。

## 当前主路径

当前 MendixStudio 微流设计器只保留：

`app-web route -> MendixStudioApp -> MicroflowResourceEditorHost -> MendixMicroflowEditorEntry -> MicroflowEditor -> FlowGramMicroflowCanvas`

旧独立编辑器壳、旧 schema 前端迁移入口、旧 graph adapter 与旧 P0 runtime fallback 均不再作为页面运行路径存在。内联主线的验收证据必须落到 `MicroflowEditor`、FlowGram renderer、inline edit、node-inline、runtime trace 与 quick-fix 相关测试上。

## 证据矩阵（核心主线）

| 要求 | 状态 | 当前证据 |
|---|---|---|
| 单击节点仅选中，不打开右侧面板主路径 | 已覆盖 | `MicroflowEditor` 统一 dock model；selection 只更新当前选择态 |
| 底部 Debug 默认隐藏，不因选中/运行自动弹出 | 已覆盖 | `bottomDockMode` 默认 `collapsed` |
| 双击节点进入展开态 | 已覆盖 | `flowgram-node-renderer-interaction.spec.tsx` |
| 字段内联提交走统一事件链 | 已覆盖 | `flowgram-node-renderer-interaction.spec.tsx`、`InlineNodeEditor.spec.tsx` |
| 连线标签内联编辑（Enter/Esc/Blur） | 已覆盖 | `flowgram-line-renderer-interaction.spec.tsx` |
| 运行后 trace 投影到节点/连线 | 已覆盖 | `FlowGramMicroflowNativeCanvas.runtime.spec.ts`、`runtime-edge-state.spec.ts` |
| 失败节点自动展开错误详情 | 已覆盖 | `derive-node-inline-config.spec.ts`、`FlowGramMicroflowNativeCanvas.runtime.spec.ts` |
| 后续成功运行清理失败 inspect 态 | 已覆盖 | `runtime-edge-state.spec.ts` 与 FlowGram runtime 投影测试 |
| 节点 inline 配置派生 | 已覆盖 | `node-inline/derive-node-inline-config.spec.ts` |
| ContextVariablePicker 可内联复用 | 已覆盖 | `inline-edit/shared/ContextVariablePicker.tsx` 与属性面板复导出 |
| ConditionBuilder 可内联复用 | 已覆盖 | `inline-edit/shared/ConditionBuilder.tsx` 与属性面板复导出 |

## 尚未完全闭环（需继续）

| 项目 | 状态 | 缺口 |
|---|---|---|
| 用户给出的 30+ 细项逐条可视验收 | 部分 | 目前以组件/单测为主，缺少端到端逐条映射报告 |
| Approval 节点完整编辑能力（若业务启用） | 部分 | 已有 inline 派生与类型支持，需按真实 schema 场景补集成测试 |
| 运行态详情“字段级 quick fix”全链路 | 部分 | 有错误块与建议结构，需补真实交互提交与回滚验证 |
| Dock 主路径浏览器验收 | 部分 | 需要在真实 `/space/:spaceId/mendix-studio/:appId` 路由补一次新建、打开、编辑、保存、运行 smoke |

## 本地验证建议

- `pnpm --dir src/frontend --filter @atlas/microflow exec vitest run src/flowgram/flowgram-node-renderer-interaction.spec.tsx src/flowgram/flowgram-line-renderer-interaction.spec.tsx src/flowgram/FlowGramMicroflowNativeCanvas.runtime.spec.ts src/flowgram/runtime-edge-state.spec.ts src/inline-edit/InlineNodeEditor.spec.tsx src/node-inline/derive-node-inline-config.spec.ts`
- `pnpm --dir src/frontend --filter @atlas/microflow run typecheck`
- `pnpm --dir src/frontend --filter @atlas/microflow run check-legacy`

## 下一步最小闭环建议

1. 补节点类型交互回归：展开、编辑、commit、undo/redo。
2. 补“不开 Debug/右侧面板完成排错与常用配置”的端到端用户流验证。
3. 补真实 Studio 路由 smoke：新建/打开微流、拖拽节点、删除连线、属性编辑、保存、校验、运行。
