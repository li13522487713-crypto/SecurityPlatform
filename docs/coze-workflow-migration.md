# Coze Workflow 迁移说明

## 目标

本文记录 Atlas 工作流编辑页向 `D:\Code\coze-studio-main` 源码结构靠拢时的迁移来源、Atlas 化改造边界与当前保留策略。

## 源码来源

- Coze 参考目录：
  - `frontend/packages/workflow/playground`
  - `frontend/packages/project-ide/biz-components/src/resource-folder-coze`
  - `frontend/packages/project-ide/biz-workflow`
- Atlas 落地目录：
  - `src/frontend/packages/module-workflow-react`
  - `src/frontend/packages/workflow-editor-react`
  - `src/frontend/packages/coze-shell-react`
  - `src/frontend/apps/app-web/src/app`

## 本次 Atlas 化改造

- 保留 Atlas 现有后端契约：
  - 工作流详情、草稿保存、发布、版本、试运行、Trace、变量仍走 `workflowV2Api`
  - 路由保持 `work_flow/:id/editor` 与 `chat_flow/:id/editor`
- 前端外壳改造为 Coze 风格结构：
  - 全局双侧导航继续由 `@atlas/coze-shell-react` 承担
  - 工作流页新增 Coze 风格资源栏、引用关系栏、顶部工作区标签、右侧工作区宿主
  - 当前主编辑器仍是 `@atlas/workflow-editor-react`
  - `@coze-workflow/playground` 已在仓内保留并作为后续唯一目标内核，正在通过 adapter 收敛主入口
- 未直接迁入 Coze 的内容：
  - Coze 的空间 store、项目 IDE 框架、发布体系、引用权限体系、IDL 与远程 worker
  - Atlas 当前没有真实数据源支撑的插件/数据资源操作

## 当前主链状态

- 当前真实运行主链：
  - `app-web -> module-workflow-react -> workflow-editor-react -> /api/v2/workflows*`
- 当前已落地的 Host 收口方向：
  - `app-web` 不再直接承载 Coze 源码图，工作流 Coze 分支只负责跳转到独立 `src/coze-workflow-host`
  - `src/coze-workflow-host` 的标准运行策略优先对齐 `D:\Code\coze-studio-main` 的 Rush 子空间与 `rush deploy`
  - `@coze-agent-ide/space-bot` 保持原生包形态，不再走轻量化替代路线
- 当前已落地的收敛动作：
  - 单节点调试改为无伪 `Entry/Exit` 的最小真实子图
  - Trace / debug-view / node detail 已回接到 Atlas 编辑器面板
  - CanvasSchema 转换已抽出独立 adapter：`@atlas/workflow-core-react/canvas-schema-adapter`
- 当前尚未完成的关键事项：
  - `module-workflow-react` 还未正式切到 `@coze-workflow/playground`
  - Coze `node-registries / nodes-v2 / test-run-kit` 仍未成为 App 主入口的唯一节点定义来源

## 当前兼容策略

- 左侧“资源/引用关系”视觉和交互按 Coze 组织，但只接 Atlas 已存在的数据：
  - Workflow / Chatflow 列表来自 Atlas 工作流列表接口
  - 设置项驱动 Atlas 编辑器已有的变量、Trace、试运行、问题、调试、添加节点面板
  - 插件、数据资源当前只做显式空态展示，不制造假能力
- “用户界面”标签已纳入工作区结构，但仍保留迁移中状态提示，避免误导为已完整可用
- 原生 Coze Workflow Host 通过 `/api/workflow_api/*` 与 Atlas 对接：
  - 前端不再通过 provider/shim 伪装 `workflowApi`
  - 后端兼容层统一转发到 `/api/v2/workflows*` 与现有服务

## 后续建议

- 若要继续提高一致性，优先补齐以下方向：
  - 先完成 `module-workflow-react -> Coze playground adapter` 主入口切换
  - 再把资源库插件与数据资源的 Atlas 接口接到 Coze editor 真实资源服务
  - 最后才做视觉细节对齐与面板布局收口

## Round 2 落仓约束

- 第 2 轮不再把工作流当作孤立迁移点，而是把它纳入 `Atlas.Application.AiPlatform`、`Atlas.Domain.AiPlatform`、`module-studio-react`、`module-explore-react`、`module-workflow-react` 的统一规划。
- 工作流后续需要和以下能力一起落地：
  - 智能体 IDE
  - 应用 IDE
  - 资源库
  - 项目会话模板
  - 发布记录与 Connector
  - AppHost 运行态与 OpenAPI
- 详细说明见：
  - [Coze Atlas Round 2 落仓实施说明](./plan-coze-atlas-round2.md)
  - [AI Platform ER 草案](./ai-platform-er.md)
