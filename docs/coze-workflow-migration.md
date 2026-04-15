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
- 前端工作流内核改造为 Coze 真源：
  - `src/frontend/packages/workflow/**` 与 `src/frontend/packages/workflow/adapter/**` 以 `D:\Code\coze-studio-main\frontend\packages\workflow\**` 为真源
  - `@coze-workflow/playground` 与 `@coze-workflow/playground-adapter` 成为当前工作流编辑主链唯一内核
  - `@atlas/module-workflow-react` 不再承载自研 workflow sidebar / editor shell，只保留工作区宿主桥接
  - `@atlas/workflow-core-react`、`@atlas/workflow-editor-react` 保留包名，继续作为 Atlas facade 对外稳定导出
- 未直接迁入 Coze 的内容：
  - Coze 的空间 store、项目 IDE 框架、发布体系、引用权限体系、IDL 与远程 worker
  - Atlas 当前没有真实数据源支撑的插件/数据资源操作

## 当前主链状态

- 当前真实运行主链：
  - `app-web -> @atlas/module-workflow-react -> @coze-workflow/playground-adapter -> @coze-workflow/playground -> /api/workflow_api/*`
- 当前宿主分工：
  - `app-web` 继续作为唯一主宿主，承载登录、组织/工作区、返回导航和鉴权上下文
  - `@atlas/module-workflow-react` 只负责把 `workflowId / mode / spaceId / returnUrl / backPath` 映射到 Coze workflow 页面
  - 工作流内部资源侧栏、节点声明、test-run、变量、toolbar 不再由 Atlas 自研壳复制实现

## 当前兼容策略

- 原生 Coze Workflow Host 通过 `/api/workflow_api/*` 与 Atlas 对接：
  - 前端不再通过 Atlas workflow editor provider/shim 伪装主编辑能力
  - 后端兼容层统一转发到 `/api/v2/workflows*` 与现有服务

## 后续建议

- 若要继续提高一致性，优先补齐以下方向：
  - 继续补齐 Coze workflow 真源实际依赖到的资源、插件、数据库、知识库接口兼容层
  - 收敛不再参与主链的 Atlas workflow 自研文件，只保留 facade 和桥接入口
  - 最后再做非主链文件的清理与包级测试扩展

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
