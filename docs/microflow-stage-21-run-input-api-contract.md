# Microflow Stage 21 - Run Input Panel & Runtime API Contract

## 1. Scope

本轮完成 Run 入口、Run input panel、参数表单生成、参数类型转换、Stage 20 validation gate、真实 run API 调用、运行响应展示，以及按 microflowId 隔离运行输入/结果的基础模型。

本轮不做完整执行引擎、step debug、trace 可视化高亮、运行历史完整页面、表达式执行引擎、schema migration，也不新增 mock API 或孤立 demo 页面。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/StudioEmbeddedMicroflowEditor.tsx` | 修改 | 将 HTTP `runtimeAdapter` 传入嵌入式编辑器，使 Workbench 中 Run 使用真实后端 test-run 契约。 |
| `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx` | 修改 | Run toolbar 接入 validation gate、dirty Save & Run、真实 test-run 请求和输入状态按 microflowId 保存。 |
| `src/frontend/packages/mendix/mendix-microflow/src/debug/MicroflowTestRunModal.tsx` | 修改 | 升级为 Run 输入面板，展示 schema/version、参数表单、运行选项、结果/错误摘要。 |
| `src/frontend/packages/mendix/mendix-microflow/src/debug/run-input-model.ts` | 新增 | 参数模型、类型转换、输入校验、请求 DTO 构建、gate 与 A/B 隔离 helper。 |
| `src/frontend/packages/mendix/mendix-microflow/src/debug/__tests__/run-input-model.test.ts` | 新增 | 覆盖参数表单模型、required/number/boolean/list 转换、请求 DTO、gate 与 A/B 隔离。 |
| `src/frontend/packages/mendix/mendix-microflow/src/debug/index.ts` | 修改 | 导出 Stage 21 run input helper。 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 P2 运行输入面板、运行 API 契约、DTO、结果展示和 validation gate 状态。 |

## 3. Runtime API Contract

| 能力 | 前端 adapter | 后端 API | 请求 DTO | 响应 DTO | 当前状态 | 本轮处理 |
|---|---|---|---|---|---|---|
| Test run | `runtimeAdapter.testRunMicroflow` / `apiClient.testRunMicroflow` | `POST /api/microflows/{id}/test-run` | `{ schema, input, options }`；前端 request 还带 `microflowId` 用于路径选择 | `MicroflowApiResponse<{ session }>`，前端归一为 `{ runId, status, startedAt, durationMs, frames, error, session }` | 已存在 | Workbench 嵌入路径接通真实 runtimeAdapter；Run 面板提交真实请求。 |
| Run | 无独立 adapter | 当前无 `POST /api/microflows/{id}/run` | 不适用 | 不适用 | 不存在 | 本轮以已有 `test-run` 为准，不凭空新增路径。 |
| Validate | `validationAdapter.validate` / `validateMicroflowSchema` | `POST /api/microflows/{id}/validate` | `{ schema, mode, includeWarnings, includeInfo }` | `{ issues, summary }` | 已存在 | Run 前复用 Stage 20 本地 + 服务端 validation gate。 |
| Publish | `resourceAdapter.publishMicroflow` / `runtimeAdapter.publishMicroflow` | `POST /api/microflows/{id}/publish` | `{ version, description, confirmBreakingChanges, force }` | `MicroflowPublishResultDto` | 已存在 | 仅盘点，不改发布。 |
| Version | `resourceAdapter.getMicroflowVersions` 等 | `GET /api/microflows/{id}/versions` 等 | 路径参数 | version summary/detail/diff | 已存在 | 仅盘点，不改版本。 |

后端当前返回真实 `MicroflowTestRunService` 结果。执行能力仍以现有 test-run runner 为准；若返回 unsupported node、validation failed、500 等错误，前端只展示真实返回，不伪造成功。

## 4. Run Input Model

参数来源优先级：

| 来源 | 策略 |
|---|---|
| `schema.parameters` | 首选，作为请求 `input` key 的权威来源。 |
| Parameter nodes | 当 `schema.parameters` 为空时 fallback，并显示 warning。 |
| 两者不一致 | 使用 `schema.parameters`，面板显示不一致 warning。 |

类型映射：

| 类型 | 控件 | 转换 |
|---|---|---|
| `string` / `enumeration` | text input | `String(value)` |
| `integer` / `long` | text number input | 转整数，非法阻止 Run |
| `decimal` | text number input | 转 number，非法阻止 Run |
| `boolean` | true/false select | 转 boolean |
| `dateTime` | text input | 保留 ISO/string |
| `object` / `fileDocument` / `json` | JSON textarea | JSON parse，非法阻止 Run |
| `list` | JSON textarea | 必须 JSON array |
| `unknown` | JSON textarea | 尝试 JSON parse，失败阻止 Run |
| `void` / `binary` | readonly | 不作为可编辑输入 |

required 参数为空会在面板内阻止 Run。`exampleValue` 和简单 `defaultValue.raw` 会作为初始值。输入状态以 `runInputsByMicroflowId` 保存，避免 A/B 微流互相污染。

## 5. Run Gate Strategy

点击工具栏 Run 时先执行 `validateForMode(schema, "testRun")`，本地有 error 或服务端 validate 返回 error 时打开 Problems 面板并阻止打开运行流程。点击面板内 Run 时再次执行同一 gate，防止打开面板后 schema 变化绕过校验。

面板还会执行 required 和 type coercion 校验。任一输入错误都会阻止 API 调用。warning 不阻止运行，但会提示用户。

## 6. Save & Run Strategy

本轮策略为：dirty schema 不静默运行旧版本。工具栏和面板显示 `Save & Run`；用户点击后先执行 Stage 20 save gate，成功保存当前 draft 后再调用 `POST /api/microflows/{id}/test-run`。保存失败、validation error 或后端保存错误都会阻止运行。

请求中仍携带当前 `schema`，同时路径使用 active `microflowId`，`input` 使用参数名到转换后值的字典。后端以现有 DTO 接收 `{ schema, input, options }`。

## 7. Result Display Strategy

Run 面板展示：

| 字段 | 展示策略 |
|---|---|
| `status` | 按后端 session status 显示 success/failed/cancelled 等。 |
| `runId` | 展示 session id。 |
| `durationMs` | 由 startedAt/endedAt 或 adapter duration 计算。 |
| `output` | JSON 原样展示。 |
| `error` | 展示 error code/message，不改写为成功。 |
| `logs` | JSON 摘要展示，并在底部 Debug 面板完整展示。 |
| `nodeResults` | 由 trace frame output 汇总展示。 |

底部 Debug 面板继续展示 trace、输入/输出、变量、日志和错误。本轮不做 trace 高亮或历史页面。

## 8. Isolation Strategy

输入状态使用 `runInputsByMicroflowId`，helper 中提供 `runResultByMicroflowId`、`runErrorByMicroflowId`、`activeRunIdByMicroflowId` 的隔离更新模型。编辑器嵌入路径按 `microflowId:schemaId:version` remount，切换真实微流时不会把 A 的输入/结果带到 B。测试覆盖 A/B 输入与结果分别更新，不串数据。

## 9. Verification

自动测试：

- `run-input-model.test.ts` 覆盖参数表单模型、required 缺失、number 非法、boolean/list 转换、请求 DTO、validation/input/dirty gate、A/B 输入与结果隔离。

手工验收建议：

- 打开 `/space/:workspaceId/mendix-studio/:appId`，打开真实微流。
- 点击 Run，确认显示 schema version、microflowId 和参数输入。
- 输入非法 amount 或空 required 参数，确认不发起 Network。
- 修复输入后点击 Run，dirty 时先 Save & Run。
- 确认 Network 调用 `POST /api/microflows/{id}/test-run`，body 含 `schema`、`input`、`options`。
- 后端成功时展示 output/runId/duration/logs；失败或 unsupported 时展示真实 error。
- 切换到另一微流，确认不会显示上一个微流的输入和运行结果。
