# Microflow P1.1 Create Hotfix

## 1. Scope

本次仅修复“新建微流”在后端不可用或 API 返回错误时的可用性与错误处理，不涉及 App Explorer 大规模重构、画布保存链路或 mock API 扩展。

## 2. Root Cause

- `CreateMicroflowModal` 的 `handleSubmit` 仅有 `try/finally`，缺少 `catch`，当 `onSubmit` 抛出 `MicroflowApiException` 时会形成未处理 rejection 链路。
- HTTP adapter 会把网络异常/后端异常包装为 `MicroflowApiException`（含 `status/code/traceId/fieldErrors`），但创建弹窗未把结构化错误渲染到表单。
- 前端 name 校验允许 `_` 开头，与后端 `^[A-Za-z][A-Za-z0-9_]*$` 不一致，导致前端放行后端 422。
- moduleId 入口存在 `sales/default` 隐式默认，掩盖“缺少模块上下文”的真实错误。
- 409/422/401/403/500/network 未在创建弹窗中分流展示，用户难以自助定位问题。

## 3. Changed Files

| 文件 | 修改类型 | 说明 |
| --- | --- | --- |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/resource/CreateMicroflowModal.tsx` | 修改 | 新增 `try/catch/finally`、防重复提交、结构化错误状态、status/code/traceId/fieldErrors/retry hint 展示、moduleId 默认逻辑修正与前端校验收敛。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/components/app-explorer.tsx` | 修改 | 创建成功后自动打开新微流 tab；创建失败不再在调用方二次 Toast/抛错链重复处理；传递 `defaultModuleId/moduleOptions`。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/resource/MicroflowResourceTab.tsx` | 修改 | 去除 `sales/default` fallback，新增缺少 moduleId 的前端阻断与 name 正则一致性。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/http/http-resource-adapter.ts` | 修改 | 新增开发环境创建请求诊断日志（method/path/apiBaseUrl/workspaceId/moduleId/status/code/traceId/header/payload摘要）。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/http/microflow-api-client.ts` | 修改 | 增加 RFC ProblemDetails 兼容解析（`title/detail/status/traceId`）。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/adapter/http/microflow-api-error.ts` | 修改 | 补充 `MICROFLOW_NAME_DUPLICATED` 文案与创建场景权限提示。 |
| `src/backend/Atlas.Application.Microflows/Services/MicroflowResourceService.cs` | 修改 | name 校验 field path 改为 `input.name`，与创建 payload 字段路径一致。 |
| `src/backend/Atlas.Application.Microflows/Exceptions/MicroflowExceptionMapper.cs` | 修改 | `UnauthorizedAccessException` 映射为 401 + `MICROFLOW_UNAUTHORIZED`。 |
| `src/frontend/package.json` | 修改 | 新增 `verify:microflow-create-hotfix` 脚本。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/resource/CreateMicroflowModal.spec.tsx` | 新增 | 前端组件/单元测试覆盖创建失败与重复提交等核心场景。 |
| `src/frontend/scripts/verify-microflow-create-hotfix.mjs` | 新增 | 最小集成测试覆盖 success/409/422/moduleId 缺失/network unavailable + trace/envelope 检查。 |
| `tests/Atlas.AppHost.Tests/Microflows/MicroflowCreateHotfixTests.cs` | 新增 | 后端创建服务与异常过滤器测试（422/409/envelope/trace header）。 |

## 4. Error Mapping

| status/code | UI message | field behavior | retry |
| --- | --- | --- | --- |
| `MICROFLOW_NETWORK_ERROR` / `MICROFLOW_SERVICE_UNAVAILABLE` | 微流服务不可用，请检查网络或后端服务。 | 表单保持打开，显示结构化错误块 | 建议重试 |
| `401` / `MICROFLOW_UNAUTHORIZED` | 登录已失效，请重新登录。 | 表单保持打开 | 否（需登录） |
| `403` / `MICROFLOW_PERMISSION_DENIED` | 当前账号无权限创建微流。 | 表单保持打开 | 否（需授权） |
| `409` / `MICROFLOW_NAME_DUPLICATED` | 同名微流已存在。 | `Name` 字段标红并提示 | 可修改名称后重试 |
| `422` / `MICROFLOW_VALIDATION_FAILED` | 使用服务端 message + 字段级错误 | 按 `fieldErrors` 回填到字段（如 `input.name/input.moduleId`） | 修正输入后重试 |
| `500` | 微流服务异常，请联系管理员。 | 显示 `traceId` 便于排查 | 视情况 |

## 5. Request Contract

`POST /api/microflows`

```json
{
  "workspaceId": "workspace-id",
  "input": {
    "name": "OrderCreate",
    "displayName": "OrderCreate",
    "description": "optional",
    "moduleId": "sales",
    "moduleName": "Sales",
    "tags": ["microflow"],
    "parameters": [],
    "returnType": { "kind": "void" },
    "returnVariableName": null,
    "security": {},
    "concurrency": {},
    "exposure": {},
    "template": "blank",
    "schema": null
  }
}
```

## 6. Verification

- 前端组件测试：
  - `onSubmit` reject 时弹窗不关闭。
  - reject 后 loading 可恢复并可再次提交。
  - 展示 `message/code/traceId`。
  - 409 映射到 Name 字段错误。
  - 422 `fieldErrors` 显示。
  - `_abc` 前端阻止提交。
  - moduleId 缺失前端阻止提交并提示上下文缺失。
  - double click 只触发一次提交。
- 最小集成测试（脚本）：
  - 创建成功。
  - 重复名称 409。
  - moduleId 缺失 422 + `fieldErrors`。
  - 非法 name 422 + `fieldErrors`。
  - 后端不可用（网络失败）探测。
  - 错误响应 envelope + `traceId` + `X-Trace-Id` 校验。
- 后端测试：
  - `CreateAsync` success / duplicate / invalid name / missing moduleId。
  - `MicroflowApiExceptionFilter` 返回 envelope 并写入 `X-Trace-Id`。
