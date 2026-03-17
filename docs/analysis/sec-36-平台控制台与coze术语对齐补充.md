# SEC-36 平台控制台与 Coze 相关文档术语对齐补充

## 1. 任务信息

- Linear：`SEC-36`（`[Impl/P0] 对齐平台控制台与 Coze 相关文档术语`）
- 所属里程碑：`P0 基线与入口收敛`
- 输入：`SEC-22`、`SEC-31`、`SEC-40`

## 2. 对齐目标

- 将平台控制台、应用工作台、运行交付面的对象语言与 Coze 能力映射使用同一套主术语。
- 保留 Coze 原词作为“外部对照别名”，不作为平台主语义。

## 3. 术语对照表（平台主语义 vs Coze 原词）

| 平台统一术语 | Coze 常见表述 | 处理策略 |
|---|---|---|
| `ApplicationCatalog` | App（目录） | 主语义使用统一术语，括号保留 Coze 对照 |
| `TenantAppInstance` | Project/App（工作区资产） | 统一为租户应用实例，Coze 语境注明别名 |
| `Workspace` | Space / Workspace | 正文统一 Workspace，首处注明 “Coze Space” |
| `WorkflowDefinition` | Workflow | 定义态必须加后缀 Definition |
| `RuntimeExecution` | Run / Trace 执行 | 运行态统一 RuntimeExecution |
| `Marketplace` | Explore | 可保留展示名“探索广场”，术语归一到 Marketplace |

## 4. 四段式入口语义对齐

| 入口层 | 平台术语 | Coze 对照语义 | 不应承载 |
|---|---|---|---|
| Platform Console | 平台治理控制台 | Admin/Governance | 应用内编辑器 |
| Tenant Console | 租户治理控制台 | Tenant Ops | 运行态终端页面 |
| App Workspace | 应用构建工作台 | Studio / IDE | 平台级资源审核 |
| Runtime | 运行交付面 | Chat/Run/Session | 定义态配置界面 |

## 5. 导航命名规范（文档层）

## 5.1 一级分组命名规则

- 平台侧：资源中心、治理中心、审计与安全、开放平台。
- 应用侧：Builder、Workflow、Knowledge、Prompt、Plugin、Settings。
- 运行侧：Runtime Pages、Task Inbox、Trace 回看。

## 5.2 禁止命名

- 禁止一级分组使用“AI 平台”同时承载平台治理与应用编辑。
- 禁止“低代码中心”承载运行监控页面。
- 禁止“流程中心”同时承载个人审批与管理后台而不分层。

## 6. 可复用到后续任务的结论

| 输出结论 | 可复用任务 |
|---|---|
| 统一术语对照表 | SEC-52、SEC-61 |
| 四段式入口语义表 | SEC-43、SEC-44、SEC-53 |
| 导航命名禁用规则 | SEC-54、SEC-63、SEC-64 |

## 7. 交付核验

- [x] 形成了平台术语与 Coze 术语的单表映射  
- [x] 明确了四段式入口语义对齐规则  
- [x] 给出后续导航改造可直接引用的命名约束
