---
name: WebApp 可移除性审计
overview: 对 Atlas.WebApp 进行严格的可移除性审计，判断其是否已满足安全删除条件，并输出完整审计结论、阻塞项清单与建议修复路径。
todos:
  - id: audit-complete
    content: Atlas.WebApp 可移除性审计（已完成）
    status: completed
  - id: page-migration-dynamic
    content: "P0: 迁移 dynamic 数据管理模块（~15 页面）到 PlatformWeb"
    status: completed
  - id: page-migration-logicflow
    content: "P0: 迁移 logic-flow 逻辑流模块（~13 页面+设计器组件）到 PlatformWeb"
    status: completed
  - id: page-migration-approval
    content: "P0: 迁移 approval 审批运营模块（~9 页面+53 组件）到 PlatformWeb"
    status: completed
  - id: build-scripts-update
    content: "P0: 更新构建脚本 build-app-package.ps1 等改为调用新项目"
    status: completed
  - id: page-migration-ai
    content: "P1: 迁移 AI 高级管理页面（~17 页面）到 PlatformWeb"
    status: completed
  - id: page-migration-lowcode
    content: "P1: 迁移 lowcode 低代码管理（~10 页面）到 PlatformWeb"
    status: completed
  - id: page-migration-visualization
    content: "P1: 迁移 visualization 可视化模块（4 页面）到 PlatformWeb"
    status: completed
  - id: cleanup-workspace
    content: "P2: 清理 pnpm-workspace/scripts/docs 中的 WebApp 引用"
    status: completed
  - id: final-removal
    content: "最终: 完成所有迁移后安全删除 Atlas.WebApp"
    status: completed
isProject: false
---

# WebApp 可移除性实施计划

本计划承接《WebApp 可移除性审计报告》的阻塞项，按 P0/P1/P2 分批实施，确保删除 `Atlas.WebApp` 前具备可回滚、可验证、可发布的工程状态。
