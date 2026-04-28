# Mendix MVP Tasks

## 已实施任务

1. 迁移 `@atlas/microflow` 到 `packages/mendix/mendix-microflow`
2. 新建 6 个 Mendix 独立包
3. 新增 app-web 路由与左导航入口
4. 资源中心微流 Tab 增加 Mendix Studio 跳转
5. 建立 Schema + Zod + Guard
6. 建立 Validator（覆盖 15 类核心规则）
7. 建立 Expression Engine（parse/infer/deps/validate/evaluate）
8. 建立 Runtime Renderer + Runtime Executor
9. 建立 Debug Trace 面板
10. 建立 Studio 5 区布局与 6 个编辑器
11. 内置采购审批示例
12. 补充单元测试与文档

## 已知限制

- Workflow Designer 当前为列表/边表格编辑（未接入图形连线画布）
- `@atlas/microflow` 高级编辑器与 Mendix Schema 的双向同步为 MVP 级集成
- Runtime Executor 为本地模拟执行，不连接真实数据库/后端引擎

## 下一步路线图

- Workflow 图形画布（React Flow/X6）
- Microflow 与 Mendix Schema 双向语义同步
- Server-side Runtime Action 执行与权限强校验
- 真实 Trace 持久化与检索
- 版本管理与迁移计划执行器
