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
- 前端改造为 Coze 风格结构：
  - 全局双侧导航继续由 `@atlas/coze-shell-react` 承担
  - 工作流页新增 Coze 风格资源栏、引用关系栏、顶部工作区标签、右侧工作区宿主
  - 编辑器内核继续复用 `@atlas/workflow-editor-react`，并增加外部面板驱动能力
- 未直接迁入 Coze 的内容：
  - Coze 的空间 store、项目 IDE 框架、发布体系、引用权限体系、IDL 与远程 worker
  - Atlas 当前没有真实数据源支撑的插件/数据资源操作

## 当前兼容策略

- 左侧“资源/引用关系”视觉和交互按 Coze 组织，但只接 Atlas 已存在的数据：
  - Workflow / Chatflow 列表来自 Atlas 工作流列表接口
  - 设置项驱动 Atlas 编辑器已有的变量、Trace、试运行、问题、调试、添加节点面板
  - 插件、数据资源当前只做显式空态展示，不制造假能力
- “用户界面”标签已纳入工作区结构，但仍保留迁移中状态提示，避免误导为已完整可用

## 后续建议

- 若要继续提高一致性，优先补齐以下方向：
  - 资源库插件与数据资源的 Atlas 接口，再接入左侧资源栏真实操作
  - 工作流编辑器顶部头部按钮与底部工具栏的细节视觉继续对齐 Coze
  - 右侧属性面板、变量面板、Trace 面板的内容布局进一步按 Coze 拆分

