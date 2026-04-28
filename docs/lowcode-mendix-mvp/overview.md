# Mendix Lowcode MVP Overview

## 实施范围

- 新增独立技术栈目录：`src/frontend/packages/mendix/`
- 迁移：`src/frontend/packages/microflow` → `src/frontend/packages/mendix/mendix-microflow`
- 新建包：
  - `@atlas/mendix-schema`
  - `@atlas/mendix-validator`
  - `@atlas/mendix-expression`
  - `@atlas/mendix-runtime`
  - `@atlas/mendix-debug`
  - `@atlas/mendix-studio-core`

## 路由与入口

- 工作空间内入口：
  - `/space/:space_id/mendix-studio`
  - `/space/:space_id/mendix-studio/:appId`
- 左导航新增 `Mendix Studio` 菜单
- 资源中心微流 Tab 增加“在 Mendix Studio 中打开”跳转

## MVP 闭环

- Domain Model Designer
- Page Builder（含组件属性与绑定）
- Microflow Designer（MVP 列表编辑 + `@atlas/microflow` 高级编辑器集成）
- Workflow Designer
- Security Editor
- Runtime Renderer + 本地 Action Executor
- Debug Trace Drawer
- 采购审批示例一键加载
