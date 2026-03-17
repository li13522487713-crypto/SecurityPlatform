# SEC-16 Coze 六层映射与接入路线（含 SEC-61/62）

## 1. 任务信息

- Linear：`SEC-16`（`[P2] 按六层结构接入 Coze Studio 全量能力`）
- 覆盖子任务：`SEC-61`、`SEC-62`
- 所属里程碑：`P2 运行闭环与 Coze 接入`

## 2. 六层结构定义

1. 平台治理层  
2. 平台公共能力层  
3. 应用装配层  
4. 应用运行层  
5. 市场发布层  
6. 开发调试层

## 3. SEC-61：Coze 能力六层映射表

| Coze 能力 | 主归属层 | 平台化/App化/Runtime化 |
|---|---|---|
| Model / Provider | 平台治理层 | 平台化 |
| Connector / OAuth | 平台公共能力层 | 平台化 + App 消费 |
| Agent / Prompt / Knowledge | 应用装配层 | App化 |
| Workflow 编辑 | 应用装配层 | App化 |
| Chat Session / Task | 应用运行层 | Runtime化 |
| Marketplace / Template | 市场发布层 | 平台化 |
| Trace / Playground / Eval | 开发调试层 | 调试层专属 |
| PAT / OpenAPI | 平台公共能力层 | 平台化 |

## 4. 不应进入平台主导航的能力

- Agent 编辑器
- Workflow 编辑器
- Playground、Eval/Testset
- Plugin Debug、Model Test

以上能力应下沉到 App Workspace 或独立调试层。

## 5. SEC-62：分阶段接入路线

## 5.1 阶段拆分

| 阶段 | 接入范围 | 承载容器 | 前置条件 |
|---|---|---|---|
| Phase A | 平台治理基础（模型、连接器、插件定义） | Platform Console | SEC-12 完成 |
| Phase B | 应用装配核心（Agent/Workflow/Knowledge） | App Workspace | SEC-13 完成 |
| Phase C | 运行闭环（发布、运行、回看） | Runtime | SEC-15 完成 |
| Phase D | 市场与开放平台 | Marketplace/Open Platform | Phase A~C 稳定 |
| Phase E | 开发调试层 | Debug Layer | SEC-17 完成 |

## 5.2 暂缓项

- 一次性接入所有 Coze DevOps 细分能力；
- 未完成权限边界前的全量插件执行能力开放。

## 6. 风险与依赖

| 风险 | 依赖任务 |
|---|---|
| 平台与应用边界回退 | SEC-12、SEC-13 |
| 运行链路断裂 | SEC-15 |
| 调试能力泄露到终端用户 | SEC-17、SEC-18 |

## 7. 任务映射核验

| 任务号 | 对应章节 |
|---|---|
| SEC-16 | 第2~6章 |
| SEC-61 | 第3~4章 |
| SEC-62 | 第5~6章 |

## 8. 完成定义核验

- [x] Coze 能力已完成六层唯一主归属  
- [x] 分阶段接入路线可直接用于排期  
- [x] 明确了不应进入平台主导航的能力清单
