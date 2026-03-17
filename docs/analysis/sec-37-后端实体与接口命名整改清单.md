# SEC-37 后端实体与接口命名整改清单

## 1. 任务信息

- Linear：`SEC-37`（`[Impl/P0] 生成后端实体与接口命名整改清单`）
- 所属里程碑：`P0 基线与入口收敛`
- 输入：`SEC-23`、`SEC-33`、`SEC-34`

## 2. 整改范围

- Domain 实体命名
- DTO/Model 命名
- Controller 与路由命名
- Service 接口命名

## 3. 立刻改清单（P0 执行）

| 现状 | 目标 | 涉及层 | 风险 | 备注 |
|---|---|---|---|---|
| `LowCodeApp` | `TenantAppInstance` | Domain/DTO/API | 高 | 先并行 DTO，后迁移主读链路 |
| `/api/v1/lowcode-apps` | `/api/v2/tenant-app-instances` | Controller/客户端 | 高 | v1 保留兼容，返回弃用提示 |
| `AppsController`（实际 AppConfig） | `ApplicationConfigsController` | Controller | 中 | 避免与应用实例概念冲突 |
| `AiWorkspace`（泛 workspace） | `Workspace`（AI 仅作为域前缀） | Domain/API | 中 | 保留兼容字段 |

## 4. 兼容保留清单（并行期）

| 现状 | 目标 | 并行策略 |
|---|---|---|
| `AppManifest` | `ApplicationCatalog` + `ApplicationRelease` | v1/v2 双栈，v1 只读 |
| `RuntimeRoute` + runtime API 混用 | `RuntimeContext` / `RuntimeExecution` | 先拆 DTO，再拆服务接口 |
| `Project`（实体与上下文混用） | `ProjectAsset` / `ProjectScope` | 请求头双写、日志双读 |

## 5. 最后清理清单（弃用窗口后）

| 清理项 | 前置条件 |
|---|---|
| 删除 `LowCodeAppsController` | 前端与三方调用迁移完成 |
| 删除 `AppManifest` 旧字段别名 | contracts 与 API v2 稳定 |
| 删除 runtime 旧命名接口 | 监控/审计口径统一 |

## 6. 高风险 API 变更单独标注

| API | 风险点 | 缓解措施 |
|---|---|---|
| `/api/v1/lowcode-apps/*` | 主链路覆盖广，前端依赖深 | 提供转发层 + 兼容响应头 |
| `/api/v1/app-manifests/*` | 发布链路与导入导出耦合 | 分阶段拆 catalog/release |
| `/api/v1/runtime/*` | 运行态与定义态接口耦合 | 先补映射文档再拆路由 |

## 7. 迁移顺序建议（后端）

1. DTO/契约并行命名；
2. Controller 增量新增 v2；
3. Service 接口重命名并提供适配器；
4. 弃用窗口统计；
5. 删除 v1 旧接口。

## 8. 交付核验

- [x] 后端实体整改项完整列出  
- [x] 高风险 API 已单独标注  
- [x] 给出“立刻改/兼容/清理”三阶段执行路径
