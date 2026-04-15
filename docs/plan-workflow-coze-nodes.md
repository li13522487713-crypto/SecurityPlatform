# Coze 工作流全量复刻实施跟踪

## 已完成

- 引擎层：
  - 画布校验器、节点声明体系、CanvasSchema 扩展
  - DAG 执行增强：Selector 分支裁剪、Loop Break/Continue、Batch 子图、Resume 续跑
  - 40+ 节点枚举补齐与执行器注册
- 节点执行器：
  - Flow/AI/Data/External/Knowledge/Database/Conversation 全量执行器落地
- API：
  - 节点目录增强、节点模板列表、Stream Resume
- 前端：
  - 菜单与路由改造（platform-web/app-web）
  - 工作流列表与编辑器三栏骨架
  - 节点面板 7 类分组 + 40+ 节点外观
  - 条件边渲染、执行态渲染、版本抽屉、调试日志面板
  - 当前 Atlas 编辑器上的动态属性面板、Trace、单节点调试、真实 node detail/debug-view 回接
  - CanvasSchema 独立 adapter 与 roundtrip 测试骨架
- 测试：
  - 新增 Workflow 节点执行器单测与 DagExecutor 场景测试
  - 新增 adapter roundtrip 测试
  - 新增单节点调试 from-node 服务级测试
  - 新增 app 端工作流 E2E 用例骨架

## 进行中

- 主编辑入口收敛：
  - `module-workflow-react` 已引入统一 `WorkflowDesignerShell`
  - 底层仍默认回落到 `workflow-editor-react`
  - 下一步目标是切到 `@coze-workflow/playground` adapter

## 待继续完善

- 把 `@coze-workflow/playground` 变成唯一目标编辑内核，停止继续扩写 `workflow-editor-react` 节点定义
- 将 Coze `node-registries / nodes-v2 / test-run-kit` 接入 Atlas API adapter
- 完成黄金样本库 10 类样本的 Atlas / Coze 双边沉淀
- 扩展前端单测覆盖率（更多节点表单与交互）
- 补全 E2E 的工作流编排端到端断言（创建-编排-运行-恢复）
- 持续对齐设计细节（与 Coze 视觉/交互逐项比对）
