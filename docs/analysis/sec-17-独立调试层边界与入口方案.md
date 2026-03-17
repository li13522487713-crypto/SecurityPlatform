# SEC-17 独立调试层边界与入口方案（含 SEC-63/64）

## 1. 任务信息

- Linear：`SEC-17`（`[P2] 建立独立开发调试层`）
- 覆盖子任务：`SEC-63`、`SEC-64`
- 所属里程碑：`P2 运行闭环与 Coze 接入`

## 2. 调试层边界定义

## 2.1 调试层承载

- Playground（Prompt/Agent 临时测试）
- Workflow Debug（节点级调试）
- Eval/Testset（评测）
- Plugin Debug / Model Test
- Trace 调试视图（开发态）

## 2.2 不属于调试层

- 终端用户 Runtime 页面；
- 平台治理审批与资源审核；
- 常规应用配置页面。

## 3. SEC-63：现状入口盘点与边界划分

| 入口 | 现状位置 | 目标归属 |
|---|---|---|
| `/ai/devops/test-sets` | 主导航 AI 分组 | 调试层 |
| `/ai/devops/mock-sets` | 主导航 AI 分组 | 调试层 |
| Workflow RunPanel | 编辑器内 | 调试层（嵌入式） |
| Runtime Trace 回看 | 运行态/审计 | 运行回看层（非调试层） |

## 4. SEC-64：入口、权限与嵌入方案

## 4.1 入口方案

| 入口名 | 所在容器 | 可见人群 |
|---|---|---|
| Debug Console | App Workspace 子入口 | 应用管理员、开发者 |
| Node Debug | Workflow Editor 内嵌 | 开发者 |
| Eval Center | Debug Console 二级页 | 开发者、测试角色 |
| Trace Debug | Debug Console 二级页 | 开发者、运维 |

## 4.2 权限策略

| 权限项 | 角色 |
|---|---|
| `debug:view` | 开发者、应用管理员 |
| `debug:run` | 开发者 |
| `debug:manage` | 平台管理员（审计可见） |

## 4.3 App Studio 嵌入策略

- 调试入口作为 App Workspace 的“开发工具”分组；
- 运行态页面不直接显示调试入口；
- 允许从运行异常页面跳转调试层（需权限）。

## 5. 越权暴露风险与防护

| 风险 | 防护 |
|---|---|
| 调试入口被终端用户看到 | 入口按角色严格控制，默认隐藏 |
| 调试可操作生产数据 | 调试数据沙箱化，默认只读生产快照 |
| Trace 调试与审计混淆 | 分离“开发 Trace”与“审计回看”视图 |

## 6. 与 Runtime 的关系

- Runtime：面向业务用户的运行交付；
- Debug Layer：面向构建者与开发者的诊断/调试；
- 两者通过 `executionId` 关联，但权限体系隔离。

## 7. 任务映射核验

| 任务号 | 对应章节 |
|---|---|
| SEC-17 | 第2~6章 |
| SEC-63 | 第3章 |
| SEC-64 | 第4~5章 |

## 8. 完成定义核验

- [x] 调试层边界与非目标边界已明确  
- [x] 入口与权限方案可直接用于实现  
- [x] 已定义调试层与 Runtime 的关系与防越权策略
