# SecurityPlatform

## 简介

安全支撑平台与基础设施支撑平台的统一解决方案，面向等保2.0合规建设，采用多租户与清晰分层架构。

## 能力概览

- 身份与权限：用户、角色、权限、菜单、部门与登录令牌。
- 资产与告警：资产台账与告警查询。
- 审计：关键操作审计记录。
- 审批流：流程定义、发起、任务处理与抄送。
- 工作流：定义、实例、事件管理。

详细能力说明见 `docs/项目能力概览.md`。

## 文档

- `docs/项目能力概览.md`
- `docs/审批流功能说明.md`
- `docs/contracts.md`
- `docs/前后端DTO对齐清单.md`
- `等保2.0要求清单.md`

## 目录结构

- `src/backend`：后端项目与分层模块。
- `src/frontend`：前端应用。
- `docs`：项目文档。

## 架构要点

- Clean Architecture 分层：Domain / Application / Infrastructure / WebApi。
- 多租户隔离与安全策略配置。
- JWT + 证书认证、审计日志、HTTP 日志、CORS 白名单。

## 构建与运行

当前未固化构建与运行命令，待补充后在此更新。
