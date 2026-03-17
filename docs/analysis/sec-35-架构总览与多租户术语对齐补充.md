# SEC-35 架构总览与多租户术语对齐补充

## 1. 任务信息

- Linear：`SEC-35`（`[Impl/P0] 对齐架构总览与多租户多应用文档术语`）
- 所属里程碑：`P0 基线与入口收敛`
- 对齐基线：`SEC-31`、`SEC-32`、`SEC-33`

## 2. 本次对齐范围

| 文档 | 本次对齐目标 |
|---|---|
| `docs/架构与产品能力总览.md` | 禁用裸词 `Application / Runtime / Workflow / DataSource` |
| `docs/多租户多应用.md` | 固化 `ApplicationCatalog / TenantApplication / TenantAppInstance` 三段表达 |

## 3. 对齐规则

## 3.1 术语替换规则

| 旧表述 | 统一表述 |
|---|---|
| App / Application（裸用） | 按上下文替换为 `ApplicationCatalog` / `TenantApplication` / `TenantAppInstance` |
| Runtime（裸用） | `RuntimeContext`（上下文）或 `RuntimeExecution`（执行实例） |
| Workflow（裸用） | `WorkflowDefinition`（定义）或 `RuntimeExecution`（执行） |
| DataSource（裸用） | `TenantDataSource` |
| Project（创作语境） | `ProjectAsset` |

## 3.2 禁止混用规则

- 禁止在同一段中同时用 `AppManifest` 与 `LowCodeApp` 表示“主应用对象”。
- 禁止将 `Project` 写成默认主链路必选上下文。
- 禁止将 `RuntimeRoute` 与 `RuntimeExecution` 混写为同类对象。

## 4. 对齐后的一致性断言

## 4.1 主链条一致

```text
Tenant -> TenantApplication -> TenantAppInstance -> Release/Runtime
```

## 4.2 数据源一致

```text
TenantDataSource 通过 Binding 与 TenantApplication 关联
```

## 4.3 工作流一致

```text
WorkflowDefinition（定义态） != RuntimeExecution（执行态）
```

## 5. 文档差异残留与处理

| 残留点 | 当前状态 | 处理计划 |
|---|---|---|
| Coze 相关文档中的 `Space/Workspace` 双词 | 部分残留 | 在 SEC-36 中统一“主术语+别名”展示规范 |
| contracts 中 `LowCodeApp` 大段契约 | 残留（兼容） | 在 SEC-38 增补 v1/v2 命名映射和弃用声明 |

## 6. 交付核验

- [x] 明确了两份核心文档的对齐目标  
- [x] 给出统一替换与禁用混用规则  
- [x] 明确残留项与后续处理归属（SEC-36/SEC-38）
