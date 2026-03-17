# SEC-38 前端路由、类型与 contracts 命名整改清单

## 1. 任务信息

- Linear：`SEC-38`（`[Impl/P0] 生成前端路由类型与 contracts 命名整改清单`）
- 所属里程碑：`P0 基线与入口收敛`
- 输入：`SEC-23`、`SEC-34`、`SEC-39`、`SEC-40`

## 2. 前端整改清单

## 2.1 路由命名

| 现状 | 目标 | 分类 | 风险 |
|---|---|---|---|
| `/apps/:appId/*` + `app-workspace-*` | `/tenant-apps/:tenantAppId/*` + `tenant-app-studio-*` | 立刻改 | 高 |
| `/r/:appKey/:pageKey` | `/runtime/:appKey/pages/:pageKey` | 兼容保留 | 高 |
| `/ai/workspace`（单数） | `/ai/workspaces`（集合） | 立刻改 | 中 |
| `/settings/*` 与 `/console/settings/*` 并存 | 统一 `/console/settings/*`，保留 redirect | 兼容保留 | 中 |

## 2.2 类型与 API 客户端命名

| 现状文件 | 目标文件 | 分类 |
|---|---|---|
| `types/lowcode.ts` | `types/tenant-app.ts` | 立刻改 |
| `services/lowcode.ts` | `services/api-tenant-apps.ts` + `services/api-application-catalogs.ts` | 立刻改 |
| runtime 泛类型 | `RuntimeContext*` + `RuntimeExecution*` | 兼容保留 |

## 2.3 菜单与文案命名

| 现状 | 目标 |
|---|---|
| AI 平台（混载） | 平台资源中心 + 应用工作台拆分 |
| 低代码中心（含回写监控） | 应用构建中心（运行监控迁出） |
| 流程中心（混载） | 审批工作台 + 流程治理中心 |

## 3. contracts 对齐清单

## 3.1 立刻对齐

| contracts 项 | 动作 |
|---|---|
| `LowCodeApp*` 模型块 | 新增 `TenantAppInstance*` 并标记旧模型 Deprecated |
| `AppManifest*` 描述 | 补充 `ApplicationCatalog` 语义说明 |
| runtime API 段落 | 增补 context/execution 分层说明 |

## 3.2 兼容保留

| 项 | 策略 |
|---|---|
| `/api/v1/lowcode-apps/*` | 保留 6 个月弃用窗口 |
| `/api/v1/runtime/*` 旧命名 | 增加 v2 路由映射表 |

## 4. 风险项与回退策略

| 风险项 | 回退方案 |
|---|---|
| 新路由未全覆盖导致 404 | 全量保留旧路由 redirect |
| 类型切换导致编译错误 | 双类型并行导出，逐页迁移 |
| contracts 未同步导致联调偏差 | 以 `docs/contracts.md` 为唯一契约源 |

## 5. 迁移波次（前端优先级）

1. API 客户端与类型双栈；
2. 路由与菜单切主路径；
3. 页面逐步切换；
4. 清理旧路由、旧类型、旧文案。

## 6. 交付核验

- [x] 前端路由整改项可直接拆开发卡  
- [x] 类型与客户端整改项可直接拆开发卡  
- [x] contracts 对齐项与弃用策略已明确
