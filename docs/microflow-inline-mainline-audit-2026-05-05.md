# Microflow 内联主线验收审计（2026-05-05）

## 目标重述
- 以节点/连线为主入口完成阅读、配置、调试。
- 右侧属性面板与底部 Debug Panel 默认不作为主路径。
- 保持 FlowGram 引擎与保存/撤销/重做/运行能力不破坏。

## 证据矩阵（核心主线）
| 要求 | 状态 | 证据 |
|---|---|---|
| 单击节点仅选中，不打开右侧面板主路径 | 已覆盖（间接） | `NativeMicroflowEditor` 无 `rightOpen` 状态；`onSelectionChange` 仅更新 selection：`src/frontend/packages/mendix/mendix-microflow/src/editor/NativeMicroflowEditor.tsx` |
| 底部 Debug 默认隐藏，不因选中/运行自动弹出 | 已覆盖（测试） | `bottomDockMode` 默认 `collapsed`；测试：`NativeMicroflowEditor.inline-events.spec.tsx` |
| 双击节点进入展开态 | 已覆盖（测试） | `flowgram-node-renderer-interaction.spec.tsx` 用例 `dispatches inline node toggle event on double click` |
| 字段内联提交走统一事件链 | 已覆盖（测试） | `atlas:microflow-inline-field-commit` 监听与提交：`NativeMicroflowEditor.tsx` + `NativeMicroflowEditor.inline-events.spec.tsx` |
| 连线标签内联编辑（Enter/Esc/Blur） | 已覆盖（测试） | `flowgram-line-renderer-interaction.spec.tsx` |
| 运行后 trace 投影到节点/连线 | 已覆盖（测试） | `NativeMicroflowEditor.inline-events.spec.tsx` + `FlowGramMicroflowNativeCanvas.runtime.spec.ts` + `runtime-edge-state.spec.ts` |
| 失败节点自动展开错误详情 | 已覆盖（测试） | `NativeMicroflowEditor.inline-events.spec.tsx`（`inspectingError`） |
| 后续成功运行清理失败 inspect 态 | 已覆盖（测试） | `NativeMicroflowEditor.inline-events.spec.tsx` |
| runtime 跨次执行不粘连（按最新 run） | 已覆盖（测试） | `runtime-edge-state.ts`（latest `runId` 过滤）+ `runtime-edge-state.spec.ts` |
| 节点 inline 配置派生（Start/End/Decision/Variable/REST/Call/Loop/Error/Default） | 已覆盖（代码+单测） | `src/frontend/packages/mendix/mendix-microflow/src/node-inline/*` + `derive-node-inline-config.spec.ts` |
| ContextVariablePicker 可内联复用 | 已覆盖（代码） | `inline-edit/shared/ContextVariablePicker.tsx` + `property-panel/selectors/ContextVariablePicker.tsx`（复导出） |
| ConditionBuilder 可内联复用 | 已覆盖（代码） | `inline-edit/shared/ConditionBuilder.tsx` + `property-panel/expression/ConditionBuilder.tsx` |

## 尚未完全闭环（需继续）
| 项目 | 状态 | 缺口 |
|---|---|---|
| 用户给出的 30+ 细项逐条可视验收 | 部分 | 目前以组件/单测为主，缺少端到端逐条映射报告 |
| Approval 节点完整编辑能力（若业务启用） | 部分 | 已有 inline 派生与类型支持，需按真实 schema 场景补集成测试 |
| 运行态详情“字段级 quick fix”全链路 | 部分 | 有错误块与建议结构，需补真实交互提交与回滚验证 |
| Debug Panel 完整“主入口移除”在壳层页面统一验证 | 部分 | `NativeMicroflowEditor` 已降级；`editor/index.tsx` 仍保留 legacy 开关路径，需要产品开关维度验收 |

## 本地验证记录（本轮）
- `pnpm --filter @atlas/microflow exec vitest run src/flowgram/flowgram-node-renderer-interaction.spec.tsx src/flowgram/flowgram-line-renderer-interaction.spec.tsx src/editor/NativeMicroflowEditor.inline-events.spec.tsx src/flowgram/runtime-edge-state.spec.ts`
- 结果：`4 files, 22 tests passed`
- `pnpm --filter @atlas/microflow run typecheck`
- 结果：通过

## 32 条验收清单对照（按用户清单）
说明：`已覆盖`=有明确代码或测试证据；`部分`=有实现但缺直接测试或端到端证据；`未覆盖`=当前未找到充分证据。

| # | 验收项 | 状态 | 证据 |
|---|---|---|---|
| 1 | 打开微流页面后，右侧属性面板默认不出现 | 已覆盖 | `NativeMicroflowEditor` 主路径无 right panel 状态机；`shellMode` 默认 `editor-native-layout` |
| 2 | 底部 Debug Panel 默认不出现 | 已覆盖 | `bottomDockMode` 默认 `collapsed`；`NativeMicroflowEditor.inline-events.spec.tsx` |
| 3 | 单击节点只选中节点 | 已覆盖 | `onSelectionChange` 仅更新 selection，且测试断言未出现 legacy property panel |
| 4 | 双击节点进入展开态 | 已覆盖 | `flowgram-node-renderer-interaction.spec.tsx` |
| 5 | 节点展开态可直接编辑输入 | 部分 | `InlineNodeEditor` / 派生字段支持；缺按每类节点输入字段交互集成测试 |
| 6 | 节点展开态可直接编辑输出 | 部分 | 同上，已支持字段提交链路；缺场景化集成测试 |
| 7 | 节点展开态可直接编辑变量 | 部分 | `InlineVariableField` + commit 链路；缺端到端变量节点覆盖 |
| 8 | 节点展开态可直接编辑判断条件 | 已覆盖 | `InlineConditionEditor.spec.tsx`、ConditionBuilder 内联复用 |
| 9 | 节点展开态可编辑 REST method/url/query/body/output | 部分 | `InlineHttpEditor` / REST 派生已在；缺完整交互回归测试 |
| 10 | 节点展开态可编辑子流程参数映射 | 部分 | call-microflow 派生与字段支持；缺专门交互测试 |
| 11 | 节点展开态可编辑审批人和审批分支 | 部分 | approval 派生与分支字段支持；缺真实 schema 集成测试 |
| 12 | 节点展开态可编辑循环变量 | 部分 | loop 派生存在；缺交互测试 |
| 13 | 节点展开态可编辑错误处理 | 部分 | error 派生/错误块存在；缺交互测试 |
| 14 | 判断节点紧凑态显示条件与 true/false/else | 已覆盖 | decision inline 派生 + node renderer summary |
| 15 | 变量节点紧凑态显示变量名/类型/表达式 | 已覆盖 | variable inline 派生 + summary |
| 16 | REST 节点紧凑态显示 method/url/关键输入输出 | 已覆盖 | rest inline 派生 + summary |
| 17 | 子流程节点紧凑态显示目标微流/入参/返回变量 | 已覆盖 | call-microflow inline 派生 + summary |
| 18 | 循环节点紧凑态显示集合/迭代变量/body/done | 已覆盖 | loop inline 派生 + summary |
| 19 | 审批节点紧凑态显示审批人/结果变量/三分支 | 部分 | approval 派生存在；缺真实节点样例验证 |
| 20 | 错误处理节点紧凑态显示 catch/error/fallback | 已覆盖 | error inline 派生 + summary |
| 21 | 点击连线标签可直接编辑分支标签 | 已覆盖 | `flowgram-line-renderer-interaction.spec.tsx` |
| 22 | 运行后画布直接显示执行路径 | 已覆盖 | runtime edge state + runtime canvas spec |
| 23 | 运行后节点直接显示输入输出 preview | 已覆盖 | `inline-runtime.ts` + `NativeMicroflowEditor.inline-events.spec.tsx` |
| 24 | 运行失败节点自动展开错误详情 | 已覆盖 | `NativeMicroflowEditor.inline-events.spec.tsx`（inspectingError） |
| 25 | 失败节点有 quick fix | 已覆盖 | quick-fix event + `createMissingFlow`/`setFieldValue` 测试 |
| 26 | 不开 Debug Panel 也能定位错误 | 部分 | 节点内联错误块/运行态已有；缺用户流端测 |
| 27 | 不开右侧属性面板也能完成常用配置 | 部分 | 内联编辑链路已在；缺“常用配置集合”端测 |
| 28 | TypeScript 无类型错误 | 已覆盖 | `pnpm --filter @atlas/microflow run typecheck` |
| 29 | lint 通过 | 部分 | 已把 `@atlas/microflow` lint 升级为主线关键文件 ESLint 并通过；但尚未覆盖包内全部 `src/**/*.{ts,tsx}` 历史存量问题 |
| 30 | 不破坏 FlowGram 拖拽/连线/缩放/选择 | 已覆盖 | `microflow-interactions.spec.ts` 已覆盖节点移动持久化、连线映射/增删、selection patch 与 position patch（不引入伪连线） |
| 31 | 不破坏保存/运行/撤销/重做 | 已覆盖 | `NativeMicroflowEditor.inline-events.spec.tsx` 已覆盖 inline commit + undo/redo、quick-fix + undo/redo、test run 后 runtime 投影与状态保持 |
| 32 | 不破坏已有 schema 兼容性 | 已覆盖 | `schema/__tests__/schema-normalizer.test.ts` 覆盖 Decision case 修复、Loop flow collection 修复、跨 collection 非法流阻塞上报、重复 ID 阻塞上报，保持兼容修复与问题可见 |

## 下一步最小闭环建议（按缺口优先级）
1. 补 `#5-13/#19`：按节点类型补最小交互回归（展开→编辑→commit→undo/redo）。
2. 补 `#26/#27`：补“不开 Debug/右侧面板完成排错与常用配置”的端到端用户流验证。
3. 补全包级 lint 收敛：逐步清理 `@atlas/microflow` 包内历史存量 ESLint 问题，再把 lint 门禁从主线关键文件扩展到全包 `src/**/*.{ts,tsx}`。
