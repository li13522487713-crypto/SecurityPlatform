# Atlas Security Platform — E2E 测试稳定操作流程

> 本文档记录了从零开始稳定进入 E2E 测试的完整操作流程。  
> 在进行 E2E 测试前 **必须** 阅读此文档并严格按步骤执行。

---

## 目录

1. [前置条件](#1-前置条件)
2. [环境准备：一次性设置](#2-环境准备一次性设置)
3. [每次测试前的启动流程（5 步走）](#3-每次测试前的启动流程5-步走)
4. [执行 E2E 测试](#4-执行-e2e-测试)
5. [测试后的收尾](#5-测试后的收尾)
6. [常见问题与排查](#6-常见问题与排查)
7. [测试文件结构参考](#7-测试文件结构参考)
8. [编写测试用例的注意事项](#8-编写测试用例的注意事项)

---

## 1. 前置条件

| 依赖项 | 最低版本 | 验证命令 |
|---|---|---|
| .NET SDK | 10.0 | `dotnet --version` |
| Node.js | 22.x | `node --version` |
| npm | 10.x | `npm --version` |
| Playwright (chromium) | 1.58+ | `npx playwright --version` |

> [!IMPORTANT]
> 所有命令均在 **PowerShell** 中执行，项目根目录为 `e:\codeding\SecurityPlatform`。

---

## 2. 环境准备：一次性设置

以下步骤只需在首次使用或环境重建时执行一次。

### 2.1 安装 Playwright 浏览器内核

```powershell
cd src/frontend/Atlas.WebApp
npx playwright install chromium
```

### 2.2 安装前端依赖

```powershell
cd src/frontend/Atlas.WebApp
npm install
```

### 2.3 恢复后端 NuGet 包

```powershell
dotnet restore Atlas.SecurityPlatform.slnx
```

### 2.4 确认数据库中已有有效授权证书

> [!CAUTION]
> **这是最关键的前置条件！** 登录页在 License 状态非 `Active` 时 **不会显示登录表单**，E2E 测试会直接失败。
>
> 必须确保 SQLite 数据库（`atlas.db`）中已存在有效的 `Active` 状态授权证书。如果是全新数据库，需要先手动通过 API 或前端上传 `.atlaslicense` 证书激活。

验证方式（后端启动后）：

```powershell
# 检查License状态 — 应返回 "status":"Active"
Invoke-RestMethod -Uri "http://localhost:5000/api/v1/license/status" | ConvertTo-Json
```

---

## 3. 每次测试前的启动流程（5 步走）

> [!WARNING]
> **必须严格按以下顺序执行**，不可跳步。后端未就绪时启动前端或测试会导致 API 代理失败。

### 步骤 1：终止残留进程

```powershell
# 终止可能残留的后端进程
Get-Process -Name "Atlas.WebApi" -ErrorAction SilentlyContinue | Stop-Process -Force

# 终止可能残留的 Node 进程（前端 dev server / Playwright）
Get-Process -Name "node" -ErrorAction SilentlyContinue | Stop-Process -Force
```

### 步骤 2：设置环境变量

```powershell
# 后端 BootstrapAdmin 账号配置
$env:Security__BootstrapAdmin__Enabled = "true"
$env:Security__BootstrapAdmin__TenantId = "00000000-0000-0000-0000-000000000001"
$env:Security__BootstrapAdmin__Username = "admin"
$env:Security__BootstrapAdmin__Password = "P@ssw0rd!"
$env:Security__BootstrapAdmin__Roles = "Admin"

# E2E 测试专用环境变量
$env:E2E_TEST_PASSWORD = "P@ssw0rd!"
$env:E2E_TEST_USERNAME = "admin"
$env:E2E_TEST_TENANT_ID = "00000000-0000-0000-0000-000000000001"
```

### 步骤 3：启动后端 API（端口 5000）

```powershell
# 在项目根目录执行，后台运行
dotnet run --project src/backend/Atlas.WebApi
```

**等待确认后端就绪**（看到 `Now listening on: http://localhost:5000` 日志），或用以下命令验证：

```powershell
# 循环等待后端健康检查通过（最多等待 60 秒）
$maxWait = 60; $waited = 0
while ($waited -lt $maxWait) {
  try {
    $resp = Invoke-RestMethod -Uri "http://localhost:5000/api/v1/license/status" -TimeoutSec 3
    Write-Host "✅ 后端已就绪" -ForegroundColor Green
    break
  } catch {
    Write-Host "⏳ 等待后端启动... ($waited s)"
    Start-Sleep -Seconds 2
    $waited += 2
  }
}
if ($waited -ge $maxWait) { Write-Host "❌ 后端启动超时！" -ForegroundColor Red }
```

### 步骤 4：启动前端 Dev Server（端口 5173）

```powershell
cd src/frontend/Atlas.WebApp
npm run dev
```

**等待确认前端就绪**（看到 `Local: http://localhost:5173/` 日志）。

> [!NOTE]
> 如果 [playwright.config.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/playwright.config.ts) 中配置了 `webServer`（`reuseExistingServer: true`），Playwright 会在检测到已有服务运行时复用它。**建议预先手动启动前端**以便观察启动状态和排查问题。

### 步骤 5：验证端到端链路畅通

```powershell
# 通过前端代理访问后端 API — 确认代理配置正常
Invoke-RestMethod -Uri "http://localhost:5173/api/v1/license/status" | ConvertTo-Json
```

预期返回 JSON 中包含 `"status": "Active"` 且 `"tenantId"` 值为有效 GUID。

---

## 4. 执行 E2E 测试

### 4.1 执行全量 E2E 测试

```powershell
cd src/frontend/Atlas.WebApp
npx playwright test --project=chromium
```

### 4.2 执行单个测试文件

```powershell
npx playwright test e2e/specs/gui-tests.spec.ts --project=chromium
```

### 4.3 执行特定测试用例（按名称）

```powershell
npx playwright test --project=chromium --grep "测试系统登录功能"
```

### 4.4 带可视化界面执行（调试推荐）

```powershell
npx playwright test e2e/specs/gui-tests.spec.ts --project=chromium --headed
```

### 4.5 使用 Playwright UI 模式（交互式调试）

```powershell
npx playwright test --ui
```

### 4.6 查看测试报告

```powershell
npx playwright show-report
```

---

## 5. 测试后的收尾

```powershell
# 可选：停止前端和后端
Get-Process -Name "node" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "Atlas.WebApi" -ErrorAction SilentlyContinue | Stop-Process -Force
```

测试产物目录：
- **截图（失败时）**：`src/frontend/Atlas.WebApp/test-results/`
- **Trace（首次重试时）**：`src/frontend/Atlas.WebApp/test-results/`
- **报告**：`src/frontend/Atlas.WebApp/playwright-report/`

---

## 6. 常见问题与排查

### ❌ 问题 1：登录页不显示登录表单，只显示"请先激活有效的授权证书"

**原因**：数据库中无有效 License 或 License 已过期。  
**解决**：
1. 检查 `GET /api/v1/license/status` 返回的 `status` 字段
2. 如果不是 `Active`，需要通过 `POST /api/v1/license/activate` 上传有效证书
3. 确认后刷新页面

### ❌ 问题 2：Playwright 报 `browserType.launch: Executable doesn't exist`

**原因**：Chromium 浏览器内核未安装。  
**解决**：
```powershell
npx playwright install chromium
```

### ❌ 问题 3：测试超时，页面一直无法加载

**原因**：后端未启动或前端 dev server 未启动。  
**解决**：
1. 确认后端 `http://localhost:5000` 可访问
2. 确认前端 `http://localhost:5173` 可访问
3. 确认前端 `vite.config.ts` 中 `/api/v1` 代理指向 `http://localhost:5000`

### ❌ 问题 4：登录后跳转地址不匹配导致 `waitForURL` 超时

**原因**：`waitForURL` 的匹配模式与实际跳转地址不一致。  
**实际行为**：登录成功后跳转到 `/console`。  
**解决**：在测试中使用 `await page.waitForURL(/\/console/, { timeout: 15000 })` 或 `**/console**`。

### ❌ 问题 5：Placeholder 文本定位失败

**原因**：实际页面的 placeholder 文本与测试代码中写的不一致。  
**实际 placeholder 值**（以 `LoginPage.vue` 为准）：
| 字段 | Placeholder |
|---|---|
| 账号 | `手机号 / 邮箱 / 用户名` |
| 密码 | `请输入密码` |
| 租户 | `租户 / 组织 ID 来自授权证书`（readonly，由证书自动绑定） |

### ❌ 问题 6：环境变量 `E2E_TEST_PASSWORD` 缺失

**原因**：`auth.fixture.ts` 中的 `loginAsAdmin` 需要此变量。  
**解决**：在执行测试的同一 PowerShell 会话中设置 `$env:E2E_TEST_PASSWORD = "P@ssw0rd!"`。

---

## 7. 测试文件结构参考

```text
src/frontend/Atlas.WebApp/
├── playwright.config.ts          # Playwright 配置（testDir、webServer、projects）
├── e2e/
│   ├── fixtures/
│   │   └── auth.fixture.ts       # 登录 fixture（提供 loginAsAdmin 方法）
│   └── specs/
│       ├── smoke.spec.ts         # 冒烟测试（登录页渲染验证）
│       ├── gui-tests.spec.ts     # GUI 功能测试（角色等模块 CRUD）
│       └── gate-r1-*.spec.ts     # Gate 验收截图取证
└── test-results/                 # 测试产物输出目录
```

---

## 8. 编写测试用例的注意事项

### 8.1 登录流程最佳实践

推荐使用 **API 登录 + Token 注入** 方式（参考 `gate-r1-productization.spec.ts`），避免每个测试都走 UI 登录：

```typescript
// 通过 API 获取 Token
const loginResp = await page.request.post("/api/v1/auth/token", {
  headers: {
    "Content-Type": "application/json",
    "X-Tenant-Id": tenantId,
  },
  data: { username, password },
});
const { accessToken, refreshToken } = (await loginResp.json()).data;

// 注入到浏览器存储
await page.goto("/login");
await page.evaluate(({ accessToken, refreshToken, tenantId }) => {
  sessionStorage.setItem("access_token", accessToken);
  localStorage.setItem("refresh_token", refreshToken);
  localStorage.setItem("tenant_id", tenantId);
}, { accessToken, refreshToken, tenantId });

// 直接跳转到目标页
await page.goto("/console");
```

### 8.2 元素定位优先级

1. **`getByRole`**（最优先）— `getByRole("button", { name: "登录" })`
2. **`getByText`** — `getByText("角色管理", { exact: true })`
3. **`getByPlaceholder`** — `getByPlaceholder("手机号 / 邮箱 / 用户名")`
4. **`getByLabel`** — `getByLabel("角色名称")`
5. **`locator` CSS 选择器**（最后手段）— `locator(".ant-table")`

### 8.3 等待策略

- ❌ **禁止使用** `page.waitForTimeout(固定毫秒)` 作为主要等待手段
- ✅ **推荐使用** `expect(locator).toBeVisible({ timeout: N })` 等待元素出现
- ✅ **推荐使用** `page.waitForURL(pattern, { timeout: N })` 等待路由跳转
- ✅ **推荐使用** `page.waitForLoadState("networkidle")` 等待网络空闲

### 8.4 测试数据标识

- 所有 E2E 测试创建的数据 **必须** 使用明确前缀（如 `E2E_`、`GUI_AUTO_`）
- 测试结束后 **必须** 清理测试数据（在测试用例末尾删除创建的记录）
- 避免依赖数据库中的已有数据，测试数据应自给自足
