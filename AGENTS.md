# Repository Guidelines

All assistant responses must be in Chinese.

## Project Structure & Module Organization

Current contents are documentation-only:

- `等保2.0要求清单.md` — primary checklist for 等保2.0 requirements.

Planned architecture is C# (.NET 10) backend + Vue 3 frontend, with SqlSugar ORM and SQLite. When code is added, keep server and client separate (for example `src/backend/` and `src/frontend/`) and place shared documents under `docs/`. Update this guide if actual paths differ.

Design must follow Clean Architecture. Separate domain, application, infrastructure, and presentation layers with clear dependencies toward the core. Keep the layering explicit in folder structure and documentation.

The system consists of an infrastructure support platform and a security support platform. Design both as cohesive bounded contexts with explicit integration points and shared contracts documented under `docs/`. Treat security as a first-class capability across both platforms.

## Current Architecture Summary

- Solution: `Atlas.SecurityPlatform.slnx` with clean architecture layering under `src/backend`.
- Core: `Atlas.Core` holds shared models (API response, pagination), tenancy context, base entities.
- Domain: `Atlas.Domain` + module domains (`Assets`, `Audit`, `Alert`) with tenant-scoped entities.
- Application: module-specific DTOs, validators, and mapping profiles; AutoMapper configured via DI.
- Infrastructure: SqlSugar + SQLite, Snowflake ID generator (IdGen), NLog integration, module query services.
- Web API: ASP.NET Core controllers, JWT + certificate auth, CORS whitelist, HTTP logging, tenant middleware.
- Frontend: Vue 3 + Vite + Ant Design Vue + Router in `src/frontend/Atlas.WebApp`, dev proxy to backend.
- Contracts: `docs/contracts.md` defines unified response and pagination models for frontend/backend.

## Build, Test, and Development Commands

No build/test commands are defined yet. Once code is added, document the exact commands. Examples only:

```bash
dotnet build
dotnet test
dotnet run --project src/backend
npm install
npm run dev --prefix src/frontend
```

Quality gate: the build must be 0 errors and 0 warnings. Configure toolchains and CI to fail on any warning once established.

## Coding Style & Naming Conventions

Documentation should use clear headings (`#`, `##`, `###`) without skipping levels, short sentences, and bullet lists for requirements. Keep filenames descriptive and consistent with existing patterns (for example `等保2.0要求清单.md`).

For .NET 10: 4-space indentation, PascalCase for types/public members, camelCase for locals/fields. For Vue 3: 2-space indentation, `kebab-case` for component file names, `PascalCase` for component names. Add formatters/linters (for example `dotnet format`, `eslint`, `prettier`) and record the exact tools once configured.

Emphasize secure coding practices and object-oriented design. Prefer clear, testable abstractions; avoid unnecessary patterns, layers, or generalized frameworks.

Asynchronous coding is mandatory. Define async interfaces and implementations for Controllers and Services, and always use async/await for I/O. Database access must use the repository pattern; direct data access from Controllers is not allowed.

## 前后端约束

- 后端：禁止反射、动态类型/`dynamic`、运行时编译或表达式树生成等弱类型特性；必须使用强类型 DTO、实体、配置对象和接口，所有公共 API 输入输出都需显式类型声明与验证。
- 前端：禁止使用 `any`、`unknown` 或运行时 `eval`/动态注入脚本；必须使用 TypeScript 全量类型标注，组件 props/emit/状态均需强类型定义，API 客户端与接口契约保持类型对齐。
- 合同：前后端共享的数据契约需集中于 `docs/contracts.md` 并保持与实现同步，修改契约时同步更新类型定义与相关校验。

## API Testing Artifacts

Standardize HTTP test files. On every new or modified API endpoint, create or update a `*.http` file where `*` is the controller name (for example `Bosch.http`). The `.http` file must include requests that cover the affected endpoints.

## 控制器规范（RESTful + 版本控制）

- 控制器必须遵循 RESTful 风格：资源名用复数、路径表示资源层级，HTTP 动词表达操作语义（GET/POST/PUT/PATCH/DELETE）。
- 禁止在路径中使用动词（例如 `/create`、`/update`），改用标准动词与语义化路径。
- 统一 API 版本控制：所有 API 路由必须包含版本前缀（例如 `api/v1`）。新增版本时保持向后兼容或明确弃用策略。
- 版本并行策略：同一资源允许 `v1`/`v2` 并行存在，新增版本必须保持旧版本可用，除非明确进入弃用期。
- 弃用流程：发布新版本时同步标记旧版本为 Deprecated，并给出至少 6 个月的弃用窗口；窗口期内不再新增旧版本功能，但允许安全修复与关键缺陷修复。
- 终止策略：弃用窗口结束后方可移除旧版本路由，移除需在变更日志与发布说明中显式告知。

## Testing Guidelines

No test framework is configured. When tests are added, document:

- The .NET framework (for example `xUnit`) and the Vue stack (for example `vitest`).
- Naming patterns (for example `*Tests.cs`, `*.spec.ts`).
- Commands for unit and integration tests.

## Commit & Pull Request Guidelines

There is no Git history available in this repository. If you initialize version control, adopt a clear convention (for example `docs: update checklist` or conventional commits) and document it. Pull requests should include a concise summary, linked issues/requirements, and screenshots for UI changes.

## Security & Compliance (等保2.0)

All design and implementation must comply with 等保2.0 requirements. Treat security controls as non-optional and document how each feature satisfies relevant control points. Avoid storing secrets in the repo; use environment variables or secure secret stores. For SqlSugar + SQLite, enforce least-privilege data access and encrypt sensitive fields at rest where required by the checklist.
