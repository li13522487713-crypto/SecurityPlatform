# Atlas Security Platform - GEMINI.md

> **MANDATORY INSTRUCTION: All AI responses must be in Chinese (回复必须是中文).**

This project is a multi-tenant security support platform designed for **MLPS 2.0 (GB/T 22239-2019 / 等保2.0)** compliance. It follows a **Clean Architecture** and supports complex features like multi-tenancy, workflow/approval engines, and low-code capabilities.

## Project Overview

- **Name:** Atlas Security Platform
- **Architecture:** Clean Architecture (Domain, Application, Infrastructure, WebApi)
- **Backend:** .NET 10.0, ASP.NET Core Web API, SqlSugar ORM, SQLite (with optional encryption).
- **Frontend:** Vue 3.5 (Composition API), Vite, TypeScript, Ant Design Vue, Pinia, Vue Router.
- **Multi-tenancy:** Logical isolation using `X-Tenant-Id` header and automatic filtering in SqlSugar.
- **Key Features:**
    - **Identity & Access:** JWT authentication, MFA (TOTP), RBAC (Roles, Permissions, Menus, Departments).
    - **Security:** CSRF protection, AES encryption, Audit logging, Input validation (FluentValidation).
    - **Workflow:** Approval flows (Definitions, Tasks, Copy-to), General workflows (WorkflowCore).
    - **Low-Code:** Dynamic tables, Form definitions, AMIS-based page runtime.
    - **System Management:** Scheduled jobs (Hangfire), Monitor (CPU/Memory/Health), Dictionary/Config management.

## Core Development Principles (Mandatory)

1. **Iterative Requirement Alignment:** Requirements must be discussed and refined throughout the development lifecycle. Never assume specifications are final without regular clarification.
2. **Implementation-Validation Loop:** A feature is only complete when its implementation, unit testing, and validation are in a closed loop. No code should exist without a verification path.
3. **Zero-Tolerance for E2E Failures:** All issues identified during E2E testing **must be fixed**. Failing E2E tests are blockers, not warnings.
4. **Empirical Verification:** Before applying a fix, reproduce the failure with a test case (Unit or E2E). A fix is only confirmed when the test passes consistently.

## Project Structure

```text
src/
├── backend/
│   ├── Atlas.Core/                 # Base abstractions, models, and shared utilities.
│   ├── Atlas.Domain/               # Core domain logic and shared entities.
│   ├── Atlas.Domain.{Module}/      # Module-specific entities (e.g., Alert, Approval).
│   ├── Atlas.Application/          # Application interfaces, DTOs, and base services.
│   ├── Atlas.Application.{Module}/ # Module-specific DTOs, Validation, and Services.
│   ├── Atlas.Infrastructure/       # Service implementations, Repositories, SqlSugar context.
│   └── Atlas.WebApi/               # Controllers, Middlewares, and API host.
└── frontend/
    └── Atlas.WebApp/               # Vue 3 Vite application.
        ├── src/                    # Main source code (layouts, pages, router, services).
        ├── vite.config.ts          # Vite configuration.
        └── package.json            # Frontend dependencies and scripts.
```

## Building and Running

### Backend

Restore and run the WebApi project:

```powershell
# Restore dependencies
dotnet restore Atlas.SecurityPlatform.slnx

# Run the WebApi
dotnet run --project src/backend/Atlas.WebApi
```

**Note:** Ensure you configure the **Bootstrap Admin** environment variables before running, as mentioned in `README.md`.

### Frontend

Navigate to the frontend directory and start the dev server:

```bash
cd src/frontend/Atlas.WebApp
npm install
npm run dev
```

The frontend defaults to `http://localhost:5173` and proxies `/api/*` to `http://localhost:5000`.

### Automated Testing & Environment Setup

#### 后端单元/集成测试 (Backend Tests)
```powershell
dotnet test --configuration Release --no-restore
```

#### 前端单元测试 (Frontend Unit Tests)
```powershell
cd src/frontend/Atlas.WebApp
npm run test -- --run
```

---

### E2E 测试稳定操作流程（Playwright）

> **重要：** 进行 E2E 测试前必须严格按以下流程操作，任何跳步都可能导致测试失败。

#### 前置条件

| 依赖项 | 最低版本 | 验证命令 |
|---|---|---|
| .NET SDK | 10.0 | `dotnet --version` |
| Node.js | 22.x | `node --version` |
| npm | 10.x | `npm --version` |
| Playwright (chromium) | 1.58+ | `npx playwright --version` |

#### 一次性环境准备

以下步骤只需在首次使用或环境重建时执行一次：

```powershell
# 1. 安装 Playwright 浏览器内核
cd src/frontend/Atlas.WebApp
npx playwright install chromium

# 2. 安装前端依赖
npm install

# 3. 恢复后端 NuGet 包
cd ../../..
dotnet restore Atlas.SecurityPlatform.slnx
```

**关键前置：确认数据库中已有有效授权证书**

登录页在 License 状态非 `Active` 时**不会显示登录表单**，E2E 测试会直接失败。
必须确保 SQLite 数据库（`atlas.db`）中已存在有效的 `Active` 状态授权证书。
验证方式（后端启动后）：
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/v1/license/status" | ConvertTo-Json
# 预期返回中包含 "status": "Active"
```

#### 每次测试前的启动流程（5 步，严格按顺序执行）

**步骤 1 — 终止残留进程：**
```powershell
Get-Process -Name "Atlas.WebApi" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "node" -ErrorAction SilentlyContinue | Stop-Process -Force
```

**步骤 2 — 设置环境变量（在同一 PowerShell 会话中）：**
```powershell
# 后端 BootstrapAdmin
$env:Security__BootstrapAdmin__Enabled = "true"
$env:Security__BootstrapAdmin__TenantId = "00000000-0000-0000-0000-000000000001"
$env:Security__BootstrapAdmin__Username = "admin"
$env:Security__BootstrapAdmin__Password = "P@ssw0rd!"
$env:Security__BootstrapAdmin__Roles = "Admin"

# E2E 测试专用
$env:E2E_TEST_PASSWORD = "P@ssw0rd!"
$env:E2E_TEST_USERNAME = "admin"
$env:E2E_TEST_TENANT_ID = "00000000-0000-0000-0000-000000000001"
```

**步骤 3 — 启动后端 API（端口 5000，必须先于前端）：**
```powershell
dotnet run --project src/backend/Atlas.WebApi
# 等待日志输出 "Now listening on: http://localhost:5000"
```

**步骤 4 — 启动前端 Dev Server（端口 5173）：**
```powershell
cd src/frontend/Atlas.WebApp
npm run dev
# 等待日志输出 "Local: http://localhost:5173/"
```

> 注：`playwright.config.ts` 中配置了 `webServer`（`reuseExistingServer: true`），
> Playwright 会在检测到已有 dev server 时复用它。建议预先手动启动以便观察状态。

**步骤 5 — 验证端到端链路：**
```powershell
# 确认前端代理到后端正常工作
Invoke-RestMethod -Uri "http://localhost:5173/api/v1/license/status" | ConvertTo-Json
# 预期：返回 JSON 中 "status" 为 "Active"
```

#### E2E 执行命令

```powershell
cd src/frontend/Atlas.WebApp

# 全量执行所有测试
npx playwright test --project=chromium

# 执行 GUI 测试全量流程 (单文件全量)
npx playwright test e2e/specs/gui-tests.spec.ts --project=chromium --ui

# 按测试名称匹配
npx playwright test --project=chromium --grep "测试系统登录功能"

# 带可视化界面（调试推荐）
npx playwright test e2e/specs/gui-tests.spec.ts --project=chromium --headed

# 交互式 UI 模式
npx playwright test --ui

# 查看测试报告
npx playwright show-report
```

#### 测试产物目录

- **截图（失败时）**：`src/frontend/Atlas.WebApp/test-results/`
- **Trace（首次重试时）**：`src/frontend/Atlas.WebApp/test-results/`
- **报告**：`src/frontend/Atlas.WebApp/playwright-report/`

#### E2E 常见问题排查

| 问题 | 原因 | 解决 |
|---|---|---|
| 登录页不显示表单，只显示"请先激活有效的授权证书" | 数据库无有效 License 或已过期 | `GET /api/v1/license/status` 检查状态，需上传 `.atlaslicense` 激活 |
| `browserType.launch: Executable doesn't exist` | Chromium 未安装 | `npx playwright install chromium` |
| 测试超时，页面无法加载 | 后端或前端 dev server 未启动 | 确认 `localhost:5000` 和 `localhost:5173` 可访问 |
| `waitForURL` 超时 | 登录后实际跳转到 `/console`，匹配模式不对 | 使用 `page.waitForURL(/\/console/, { timeout: 15000 })` |
| Placeholder 定位失败 | 页面实际 placeholder 与测试代码不一致 | 账号：`手机号 / 邮箱 / 用户名`；密码：`请输入密码`；租户为 readonly 由证书绑定 |
| `E2E_TEST_PASSWORD` 缺失 | `auth.fixture.ts` 依赖此环境变量 | 在同一 PowerShell 会话设置 `$env:E2E_TEST_PASSWORD` |

#### 测试文件结构

```text
src/frontend/Atlas.WebApp/
├── playwright.config.ts          # 配置（testDir、webServer、projects）
├── e2e/
│   ├── fixtures/
│   │   └── auth.fixture.ts       # 登录 fixture（loginAsAdmin）
│   └── specs/
│       ├── smoke.spec.ts         # 冒烟测试（登录页渲染）
│       ├── gui-tests.spec.ts     # GUI 功能测试（模块 CRUD）
│       └── gate-r1-*.spec.ts     # Gate 验收截图取证
└── test-results/                 # 测试产物输出
```

#### 编写 E2E 测试的最佳实践

**登录方式 — 推荐 API 登录 + Token 注入（避免每次走 UI 登录）：**
```typescript
const loginResp = await page.request.post("/api/v1/auth/token", {
  headers: { "Content-Type": "application/json", "X-Tenant-Id": tenantId },
  data: { username, password },
});
const { accessToken, refreshToken } = (await loginResp.json()).data;
await page.goto("/login");
await page.evaluate(({ accessToken, refreshToken, tenantId }) => {
  sessionStorage.setItem("access_token", accessToken);
  localStorage.setItem("refresh_token", refreshToken);
  localStorage.setItem("tenant_id", tenantId);
}, { accessToken, refreshToken, tenantId });
await page.goto("/console");
```

**元素定位优先级：** `getByRole` > `getByText` > `getByPlaceholder` > `getByLabel` > `locator(CSS)`

**等待策略：**
- ❌ 禁止使用 `page.waitForTimeout(固定毫秒)` 作为主要等待
- ✅ 使用 `expect(locator).toBeVisible({ timeout: N })` 等待元素
- ✅ 使用 `page.waitForURL(pattern, { timeout: N })` 等待路由
- ✅ 使用 `page.waitForLoadState("networkidle")` 等待网络

**数据隔离：** 测试数据使用 `E2E_` / `GUI_AUTO_` 前缀，测试结束后清理

---

## Product Development & E2E Debugging Workflow

### 1. 防御性编程规范 (Defensive Programming for Vue)
为了杜绝 `Cannot set properties of null (setting '__vnode')` 等异步渲染错误：
- **挂载状态检查：** 在所有异步回调（如 API 请求返回后）中，必须通过 `isMounted` 标识（利用 `onBeforeUnmount` 维护）检查组件是否仍处于挂载状态，严禁在卸载后的组件上更新响应式状态。
- **可选链与空值保护：** 在模板及计算属性（如 `customRow` 样式计算）中，对 `selectedItem` 等可能为空的对象必须使用严格的空值检查或可选链保护。
- **副作用清理：** 在组件卸载时，必须取消未完成的异步任务或清理 `debounce` 定时器。

### 2. Task-Based Development (Recommended)
... (保持不变)

## Development Conventions

### Backend
- **Layered Dependencies:** Dependency flows inward: WebApi -> Infrastructure -> Application -> Domain -> Core.
- **Naming:** Follow `Atlas.{Layer}.{Module}` naming convention.
- **Query/Command Separation:** Use `I{Context}QueryService` for read operations and `I{Context}CommandService` for write operations.
- **Validation:** Use **FluentValidation** for all incoming DTOs in the Application layer.
- **Mapping:** Use **AutoMapper** for DTO <-> Entity conversions.
- **Response Handling:** Wrap all API responses in `ApiResponse<T>` or `PagedResult<T>`.
- **Error Handling:** Use `BusinessException` for expected domain errors; they are handled by `ExceptionHandlingMiddleware`.

### Frontend
- **Composition API:** Use Vue 3 `<script setup>` with TypeScript.
- **UI Framework:** **Ant Design Vue** is the primary UI library.
- **AMIS:** Used for low-code and dynamic components.
- **API Clients:** Generated via NSwag. Run `npm run generate-api` to sync with backend changes.

## Key Documentation

- `README.md`: Quick start and deployment.
- `docs/架构与产品能力总览.md`: Comprehensive architectural and feature overview.
- `docs/contracts.md`: API request/response contracts and standard headers.
- `docs/审批流功能说明.md`: Detailed logic for the approval engine.
- `等保2.0要求清单.md`: Compliance requirements mapping.
- `CLAUDE.md`: Technical stack and coding guidelines for AI assistants.
