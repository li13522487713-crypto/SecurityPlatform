# Gate-R1 手工测试报告（12 Sprint 产品化重构）

## 1. 基本信息

- 测试日期：2026-03-08
- 测试环境：Cursor Cloud Linux（kernel 6.1.147）
- 后端地址：`http://localhost:5000`
- 前端地址：`http://localhost:5173`
- 测试账号：`admin`（tenant: `00000000-0000-0000-0000-000000000001`）
- 代码基线：`cursor/security-platform-productization-cbff`（含本次启动修复提交）

## 2. 执行范围

按 Gate-R1 要求覆盖以下链路：

1. 平台控制面（`/console`、`/console/apps`、`/console/tools`）
2. 应用工作台（`/apps/:appId/dashboard`、`/apps/:appId/settings`）
3. 运行交付面（`/r/:appKey/:pageKey`）
4. License 治理页（`/settings/license`）
5. 关键 API 补充验证（`platform/app-manifests/packages/licenses/tools/runtime`）

## 3. 测试结果总览

| 场景 | 结果 | 说明 |
|---|---|---|
| 平台控制面可访问 | 通过 | 控制台首页、应用页、工具授权页可进入 |
| 应用创建向导入口 | 通过 | `/console/apps` 可打开“新建应用”向导 |
| 应用工作台页面可达 | 通过 | `/apps/1/dashboard`、`/apps/1/settings` 均可稳定访问 |
| 运行态路由可达 | 通过 | `/r/:appKey/:pageKey` 可打开并返回运行态数据 |
| License 管理页可访问 | 通过 | 页面渲染正常，支持上传证书入口 |
| 关键 API 可用性 | 通过 | `platform/app-manifests/packages/licenses/tools/runtime` 均返回 `200 SUCCESS` |

## 4. 证据留痕

### 4.1 GUI 截图证据

目录：`docs/evidence/gate-r1-20260308/`

- `01-console-home.png`
- `02-console-apps.png`
- `03-console-tools.png`
- `04-license-center.png`
- `05-app-create-wizard.png`
- `07-app-dashboard.png`
- `08-app-settings.png`
- `09-runtime-route.png`

### 4.2 API 补充验证证据

- `docs/evidence/gate-r1-20260308/api-check-results.json`

关键结果摘要：

- `POST /api/v1/auth/token`：`200 SUCCESS`
- `GET /api/v1/license/status`：`200 SUCCESS`
- `GET /api/v1/platform/overview`：`200 SUCCESS`
- `GET /api/v1/app-manifests?PageIndex=1&PageSize=10`：`200 SUCCESS`
- `POST /api/v1/packages/export`：`200 SUCCESS`
- `POST /api/v1/licenses/offline-request`：`200 SUCCESS`
- `GET /api/v1/tools/authorization-policies?PageIndex=1&PageSize=10`：`200 SUCCESS`
- `GET /api/v1/runtime/apps/{appKey}/pages/{pageKey}`：`200 SUCCESS`

## 5. 缺陷与阻塞清单

- 已在执行阶段修复并复测通过（当前无阻塞项）：
  1. 开发环境嵌入公钥与签发证书不匹配，导致 License 无法激活。
  2. `PackageArtifact` 历史表结构 `ImportedBy` 为 `NOT NULL`，导致 `/api/v1/packages/export` 写入失败（500）。
  3. API 补测脚本查询参数命名（`PageIndex/PageSize`）与实际绑定规则不一致，导致误判 `400`。

## 6. 结论

- **Gate-R1 当前结论：通过（核心链路已打通）**  
  GUI 关键入口与 API 补证均已回归通过，当前证据可支撑平台→应用→运行→治理主链路验收。

## 7. 建议修复顺序

1. 将开发/测试环境 License 证书签发流程标准化，避免手工生成差异导致回归不稳定。
2. 补充 `packages/export` 的自动化回归用例，防止数据库历史结构差异再次触发写入异常。
