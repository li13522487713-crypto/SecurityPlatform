# SEC-22 核心文档术语对齐清单

> 任务：`SEC-22` 对齐核心文档主术语与边界描述  
> 日期：2026-03-16  
> 术语基线：`docs/analysis/unified-terminology-glossary-v1.md`

## 一、已完成更新清单

1. `docs/架构与产品能力总览.md`
   - 新增“主术语基线（SEC-22）”章节。
   - 将 `Application / Project / Workflow / DataSource` 的裸词改为受控术语表达。
   - 明确 `WorkflowDefinition` 与 `RuntimeExecution` 的定义态/运行态边界。

2. `docs/多租户多应用.md`
   - 重写为结构化版本，统一对象为 `Tenant / ApplicationCatalog / TenantApplication / TenantAppInstance / TenantDataSource / ProjectAsset`。
   - 删除历史草案式口语描述，补齐关系约束、接口行为约束与验收标准。

3. `docs/plan-平台控制台与应用数据源.md`
   - 新增“术语对齐声明（SEC-22）”。
   - 统一文中对象语义为 `ApplicationCatalog / TenantApplication / TenantAppInstance / TenantDataSource / ProjectAsset`。
   - 在变更记录中补充 SEC-22 对齐记录。

4. `docs/coze-studio-feature-atlas.md`
   - 新增 Coze 语境到平台主术语的映射声明。
   - 将项目与工作流关键域标题改为对齐命名（`ProjectAsset`、`WorkflowDefinition/RuntimeExecution`）。

5. `docs/contracts.md`
   - 新增“术语收敛与命名兼容（P0/P1/P2 基线补充）”章节。
   - 明确 `ApplicationCatalog / TenantApplication / TenantAppInstance / RuntimeContext / RuntimeExecution` 契约语义。
   - 补充 v1/v2 命名映射与 Runtime 分层约束。

6. `docs/analysis/sec-32-目标主模型关系图与边界说明.md`
   - 输出主模型关系图与四类边界角色说明。
   - 固化拥有、引用、可选绑定关系。

7. `docs/analysis/sec-10-四段式入口IA实施基线.md`
   - 输出四段式入口职责、上下文切换、路由迁移和 legacy 兼容策略。
   - 覆盖 SEC-24~28、SEC-41~48 的文档交付映射。

8. `docs/analysis/sec-11-app三段模型收敛方案.md`
   - 输出 App 三段模型定义与改造清单（SEC-49/50）。

9. `docs/analysis/sec-12-平台资源中心与双层消费模型.md`
   - 输出平台治理资源清单、双层消费模型与资源中心 IA（SEC-51/52）。

10. `docs/analysis/sec-13-app-workspace能力地图与页面下沉方案.md`
    - 输出 App Workspace 能力地图、页面下沉清单（SEC-53/54）。

11. `docs/analysis/sec-14-project可选子域规则.md`
    - 输出 Project 可选子域启用条件、生命周期与上下文字段规则（SEC-55/56）。

12. `docs/analysis/sec-18-四层资源与权限矩阵.md`
    - 输出平台/租户/应用/项目四层资源与角色权限矩阵（SEC-57/58）。

13. `docs/analysis/sec-15-runtime-release闭环方案.md`
    - 输出发布-运行-回滚-审计闭环链路（SEC-59/60）。

14. `docs/analysis/sec-16-coze六层映射与接入路线.md`
    - 输出 Coze 六层能力映射与分阶段接入路线（SEC-61/62）。

15. `docs/analysis/sec-17-独立调试层边界与入口方案.md`
    - 输出调试层边界、入口、权限与嵌入方案（SEC-63/64）。

16. `docs/analysis/sec-35-架构总览与多租户术语对齐补充.md`
    - 补充架构总览与多租户文档层面的术语替换规则与残留项处理。

17. `docs/analysis/sec-36-平台控制台与coze术语对齐补充.md`
    - 补充平台控制台与 Coze 术语对照和导航命名规则。

18. `docs/analysis/sec-37-后端实体与接口命名整改清单.md`
    - 补充后端实体/接口命名整改与 API 风险清单。

19. `docs/analysis/sec-38-前端路由类型与contracts命名整改清单.md`
    - 补充前端路由/类型/contracts 对齐清单与迁移波次。

## 二、当前统一边界（供评审引用）

- 平台目录定义：`ApplicationCatalog`
- 租户开通关系：`TenantApplication`
- 租户应用运行载体：`TenantAppInstance`
- 租户数据连接资源：`TenantDataSource`
- 创作空间：`Workspace`
- 创作资产容器：`ProjectAsset`
- 流程定义态/运行态：`WorkflowDefinition` / `RuntimeExecution`

## 三、待后续文档跟进列表

1. `docs/coze-studio-feature-inventory.md`
   - 原因：清单规模大（大量 API 与模块说明），需专项批量替换并复核上下文语义。

2. `docs/coze-studio-project-cognitive-map.md`
   - 原因：包含大量“项目结构原文映射”，需在“忠实源码目录”与“统一术语”之间采用双栏表达，避免失真。

3. `docs/coze-studio-api-inventory.md`
   - 原因：API 路径需保持原样，术语只能在“解释层”替换，需补充“原始接口名 vs 目标语义”映射表。

4. `docs/coze-studio-tech-stack-analysis.md`
   - 原因：当前以技术栈为主，需补充“对象边界章节”后再对齐主术语，不宜直接全文替换。

5. `docs/项目能力概览.md`
   - 原因：作为产品对外总览，需与 `架构与产品能力总览.md` 一并做术语联动修订。

## 四、一致性检查结论

- 本次优先文档已消除核心冲突对象的裸词主语义竞争（Application、DataSource、Workflow、Project）。
- 后续评审可优先以本清单及已更新文档作为 P0 基线；待跟进文档按本清单第三节逐步完成。
