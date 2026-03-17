# SEC-11 App 三段模型收敛方案（含 SEC-49/50）

## 1. 任务信息

- Linear：`SEC-11`（`[P1] 收敛 App 主语义并拆分定义、实例与发布对象`）
- 覆盖子任务：`SEC-49`、`SEC-50`
- 所属里程碑：`P1 模型与边界收口`

## 2. 三段对象定义

| 对象 | 主职责 | 生命周期 |
|---|---|---|
| `ApplicationDefinition`（目录定义） | 平台目录、模板、能力声明 | 平台创建 -> 版本演进 |
| `TenantApp(AppSpace)`（租户实例） | 租户开通后的应用实例与配置空间 | 开通 -> 配置 -> 停用 |
| `Release/RuntimeRoute`（发布运行） | 发布快照、路由、运行绑定 | 发布 -> 运行 -> 回滚 |

## 3. 三段关系图

```mermaid
flowchart LR
  AD[ApplicationDefinition] --> TA[TenantApp(AppSpace)]
  TA --> RL[Release]
  RL --> RR[RuntimeRoute]
```

## 4. SEC-49 交付：对象边界与约束

## 4.1 边界约束

- `ApplicationDefinition` 不保存租户运行状态。
- `TenantApp` 不直接承担平台目录审核能力。
- `Release` 必须可回滚，`RuntimeRoute` 只能指向已发布版本。

## 4.2 易混点消解

| 易混点 | 统一规则 |
|---|---|
| `LowCodeApp` vs `AiApp` | 统一为 `TenantApp` 子类型 |
| `AppManifest` vs `Release` | 前者定义态，后者发布态 |
| `Runtime` 裸词 | 拆成 `RuntimeContext`/`RuntimeExecution` |

## 5. SEC-50 交付：改造清单

## 5.1 后端实体改造

| 现状 | 目标 | 风险 |
|---|---|---|
| `LowCodeApp` | `TenantAppInstance` | 高 |
| `AiApp` | `TenantAppInstance(Ai)` | 中 |
| `AppManifest` | `ApplicationDefinition` | 高 |

## 5.2 DTO/API 改造

| 现状契约 | 目标契约 |
|---|---|
| `LowCodeAppResponse` | `TenantAppInstanceResponse` |
| `AppManifestResponse` | `ApplicationDefinitionResponse` |
| runtime 统一 DTO | context/execution 拆分 DTO |

## 5.3 前端模型改造

| 现状类型 | 目标类型 |
|---|---|
| `LowCodeApp*` | `TenantAppInstance*` |
| `AppManifest*` | `ApplicationDefinition*` |
| `Runtime*` | `RuntimeContext*` + `RuntimeExecution*` |

## 6. 分波次实施建议

1. 文档与契约先行；
2. API v2 并行；
3. 前端类型与服务双栈；
4. 数据模型迁移；
5. 旧命名退役。

## 7. 任务映射核验

| 任务号 | 对应章节 |
|---|---|
| SEC-11 | 第2~6章 |
| SEC-49 | 第4章 |
| SEC-50 | 第5章 |

## 8. 完成定义核验

- [x] 三段模型边界明确  
- [x] 后端/DTO/API/前端改造清单可直接实施  
- [x] 给出分阶段迁移顺序与风险
