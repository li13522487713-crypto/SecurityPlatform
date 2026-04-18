# Coze 低代码差距补齐 — P1 核心 UI 填实验证报告

> 范围：PLAN §P1-1 ~ §P1-6（47 组件真实实现 / 设计器骨架 / 画布 autosave+draftLock / Studio 修正 / AiChat / Yjs awareness+offline）
> 完工时间：2026-04-18

## 1. 修复项总览

| 编号 | 描述 | 状态 |
| --- | --- | --- |
| **P1-1 完成** | 47 组件真实 React 实现 + propertyPanels 元数据驱动 + AI 6 维矩阵 | ✅ 完整 |
| **P1-3 完成** | Studio Mod+S → autosave 修正、30s 兜底 autosave 循环、draftLock 心跳 | ✅ 完整 |
| **P1-5 完成** | chatflow 非 long id mock 路径改为返回 `CHATFLOW_NOT_FOUND` 错误（去 mock） | ✅ 完整 |
| **P1-6 完成** | Yjs SignalR Provider awareness 跨端同步 + Hub `SendAwareness` 方法 + offline IndexedDB persistence | ✅ 完整骨架 |
| **P1-2 部分** | propertyPanels 元数据已全部 47 组件落地；Inspector 真实三 Tab Semi UI 与 Monaco 内嵌 UI 重写留增量 | ⚠️ 协议层完整、UI 待增量 |
| **P1-4 部分** | autosave + draftLock + Mod+S 修正 + i18n 完整；模板 Tab 四类向导 / 数据 Tab 4 类数据源 UI 重写留增量 | ⚠️ 关键路径完成、模板/数据 Tab 待增量 |

## 2. 关键改动

### P1-1 47 组件真实 React 实现

- 新增 5 个 .tsx 文件：`layout.tsx` / `display.tsx` / `input.tsx` / `ai.tsx` / `data.tsx`
- 共 47 个组件实现（与 ALL_METAS 严格 1:1，单测守门）
- ComponentMeta 全部追加 `propertyPanels`（自动按 bindableProps + supportedEvents + contentParams 派生面板字段；启发式 renderer 映射 input/number/switch/color/value-source/monaco-expr/event-config/content-param）
- `runtime-types.ts` 暴露 `ComponentRenderer` / `FireEvent` / `GetContentParam` 接口；强约束：组件实现禁止直调 fetch / `/api/runtime/*`，所有外部能力通过 props 注入
- AiChat 完整：SSE 4 类事件接入 ChatflowAdapter（stream/pause/inject/regenerate）+ tool_call 气泡（折叠/展开 + 重试）+ 历史回放 + 中断按钮 + 重试按钮 + 错误显示

文件：
- `src/frontend/packages/lowcode-components-web/src/components/{layout,display,input,ai,data}.tsx`
- `src/frontend/packages/lowcode-components-web/src/components/{runtime-types,implementation-keys,index}.tsx`
- `src/frontend/packages/lowcode-components-web/src/meta/categories.ts` (propertyPanels 自动派生)

### P1-3 Studio 草稿安全（autosave + draftLock + Mod+S 修正）

- **Mod+S 修正**：之前走 snapshot（创建版本），现改为 autosave；与 `docs/lowcode-shortcut-spec.md` 第 69 行一致
- **30s 兜底 autosave**：新增 `useDraftAutosave` hook，按 30s 周期对比 schemaJson 差异写回 autosave，避免长时间无操作导致 latest 漂移
- **draftLock 心跳**：30s 周期调用 `lowcodeApi.draftLock.renew`，与服务端 60s TTL 配合保留 30s 容错；`beforeunload` 时调用 release
- **scheduleAutosave 工具函数**：500ms debounce，便于编辑组件按 prop 变更立即触发 autosave

文件：
- `src/frontend/apps/lowcode-studio-web/src/hooks/use-draft-autosave.ts`（新增）
- `src/frontend/apps/lowcode-studio-web/src/hooks/use-studio-commands.ts`（Mod+S 修正）
- `src/frontend/apps/lowcode-studio-web/src/app/studio-app.tsx`（接入 useDraftAutosave）
- `src/frontend/apps/lowcode-studio-web/src/i18n/index.ts`（新增 savedDraft / autosaveFailed 词条）

### P1-5 chatflow 去 mock

- `RuntimeChatflowService.StreamSseAsync` 中 `if (!bridged)` 分支改为：发出 `error` chunk + `final` chunk（含 `CHATFLOW_NOT_FOUND` 错误码）
- 不再产生伪 tool_call / 按字符 message 帧；不再写入伪输出
- 同步进 `RuntimeMessageLogService` 错误日志

文件：
- `src/backend/Atlas.Infrastructure/Services/LowCode/RuntimeSessionAndChatflowServices.cs`

### P1-6 Yjs awareness 跨端同步 + offline IndexedDB

- **awareness 通道**：YjsSignalRProvider 增加 `awareness` 实例 + 监听 `awareness` 事件 + 发送 `SendAwareness` Hub 调用
- 帧格式：base64 编码的 awareness 协议二进制；`removeAwarenessStates` 在 disconnect 时清理本地状态以便其它客户端立即感知"对方下线"
- 后端 `LowCodeCollabHub` 新增 `SendAwareness` 方法（与 SendUpdate 对称）：仅做权限校验 + 二进制中转，不解析 awareness 内容
- **offline 模块**：新建 `lowcode-collab-yjs/src/offline/index.ts`，封装 `y-indexeddb` 的 `IndexeddbPersistence`，db 名 `atlas-lowcode:{appId}`
- IndexedDB 不可用环境（SSR / 隐私模式）安全降级为 NoopPersistence
- package.json 新增 `y-indexeddb ^9.0.12` 依赖

文件：
- `src/frontend/packages/lowcode-collab-yjs/src/signalr-provider/index.ts`
- `src/frontend/packages/lowcode-collab-yjs/src/offline/index.ts`（新增）
- `src/frontend/packages/lowcode-collab-yjs/src/index.ts`（导出 offline）
- `src/frontend/packages/lowcode-collab-yjs/package.json`
- `src/backend/Atlas.AppHost/Hubs/LowCodeCollabHub.cs`

## 3. 验证

### 后端构建（0 警告 0 错误）
```
dotnet build Atlas.SecurityPlatform.slnx
已成功生成。
    0 个警告
    0 个错误
已用时间 00:01:08.03
```

### 后端单测（371 + P0 新增不变）
P1 后端改动仅 2 处（chatflow + Hub），未引入新单测；现有 371 单测仍全绿。

### 前端单测
```
pnpm exec vitest run packages/lowcode-components-web packages/lowcode-collab-yjs
Test Files  3 passed (3)
     Tests  14 passed (14)
```

新增 5 个 P1-1 守门单测（ComponentMeta ↔ 实现一致性 + propertyPanels 完整性）。

### 前端 i18n
```
[i18n-audit] zh missing=0, unresolved=0, autofill=0
[i18n-audit] en missing=0, unresolved=0, autofill=0
```

### 前端关键文件 lint
```
pnpm exec eslint packages/lowcode-components-web/src packages/lowcode-collab-yjs/src apps/lowcode-studio-web/src/hooks
（0 输出 / 0 警告）
```

## 4. P1 内的简化与待增量项

> 这些项属于"协议层 / 关键路径已闭环，UI 重写留增量"，不影响 P0+P1 关键安全/契约修复，但需要在后续批次或单独 task 中完成。

1. **P1-2 Inspector 三 Tab Semi UI 完整重写**：当前 Inspector 仍为只读展示；propertyPanels 元数据已为 47 组件全部落地，但 `lowcode-property-forms/renderer` 还未把 propertyPanels 转为 Semi `Form.Slot` 完整渲染。
2. **P1-2 Monaco 内嵌真实编辑器**：`lowcode-property-forms` 暂未引入 `monaco-editor` / `@monaco-editor/react` 依赖；当前表达式编辑回退到 textarea。
3. **P1-2 6 类内容参数富 UI**：`content-params` 当前只有 default 工厂，6 类（text/image/data/link/media/ai）每类独立编辑器 UI 待补。
4. **P1-2 7 子动作类型表单 + 动作链可视化**：`lowcode-editor-inspector/event-config` 当前只有 schema 级 append/move；可视化节点视图待补。
5. **P1-2 Outline 真实组件结构树**：`lowcode-editor-outline` 当前为纯函数；Studio 左侧"结构"Tab 重写为基于 Semi Tree + dnd-kit 的拖拽树待补。
6. **P1-3 dnd-kit 接入画布**：当前画布仍用原生 onDragOver/onDrop；将 `lowcode-editor-canvas/dnd` 整合到 Studio canvas-viewport 待补（不影响保存/锁/快捷键路径）。
7. **P1-3 注册表全快捷键运行时绑定**：当前 Studio 仅绑定 Esc / Delete / Mod+S；`docs/lowcode-shortcut-spec.md` 列出的 ≥40 项剩余需要在 use-studio-commands 增量接入。
8. **P1-4 模板 Tab 四类向导**：当前模板 Tab 只有列表 + 应用；页面/组件组合/模式 ABCD/行业模板四类创建向导待补。
9. **P1-4 数据 Tab 4 类数据源 UI**：工作流输出 / 数据库快捷查询 / 静态 mock / 共享数据源 4 类独立编辑器待补。
10. **P1-4 valueType 9 类**：当前 6 项；扩到 9 类对齐 PLAN（含 expression / file / image 细分）。
11. **P1-5 Studio 顶部"会话管理"抽屉**：AiChat 内已支持完整 SSE+pause/resume/regenerate；但 Studio top-toolbar 还缺会话切换抽屉入口（接 lowcode-session-adapter list/create/switch 已存在）。
12. **P1-6 Studio 启用 Yjs 协同**：awareness 通道已通；canvas-viewport 还未启用协同模式（即未实例化 YjsSignalRProvider 并切换 IHistoryProvider 为 YjsCollabHistoryProvider）。
13. **P1-6 5 浏览器并发 E2E**：留 P4 阶段做 Playwright 多 BrowserContext 演示。

## 5. 进入 P2

P1 关键路径已闭环（47 组件实现 + autosave/lock/Mod+S 修正 + chatflow 去 mock + Yjs awareness/offline）。下一步进入 P2：SDK UMD/ESM 真实双输出 + AppPublishService MinIO+CDN + sdk-playground 真实嵌入 + 严格 CSP + lowcode-mini-host Taro 工程 + 47 mini 组件 + 资产分片上传。
