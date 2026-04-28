# Validation / ProblemPanel 联调说明

第 45 轮范围只覆盖后端 Validation API 到前端 ValidationAdapter、ProblemPanel、FieldError、FlowGram validation state、Toolbar/Header、Publish/TestRun 前置校验的真实 HTTP 联调，不实现完整 Runtime、表达式执行器或完整 Mendix Validator。

## 前置条件

- 第 40 轮后端 `POST /api/microflows/{id}/validate` 已可用，返回 `MicroflowApiResponse<ValidateMicroflowResponse>`。
- 前端 `adapterMode=http` 时默认使用 HTTP ValidationAdapter；`local/mock` 模式使用本地 `validateMicroflowSchema`。
- app-web 不直接调用 validate API，不解析 `MicroflowValidationIssue`，只透传 adapterConfig / adapter bundle。

## mode 策略

- `edit`：用于手动校验与 debounce 校验。校验服务失败显示全局 warning，不标红所有节点。
- `save`：保存前校验。存在 error 或校验服务不可用时阻止保存并打开 ProblemPanel；只有 warning 时允许保存。
- `publish`：PublishModal 前置校验。error 或校验服务不可用时禁用发布；warning 可按当前发布确认策略继续。
- `testRun`：TestRun 前置校验。error 或校验服务不可用时不打开运行输入弹窗；warning 不阻止。

## Issue 显示与定位

后端 issue 字段使用统一契约：`id/severity/code/message/source/objectId/flowId/actionId/fieldPath`。ProblemPanel 展示 severity、code、message、source 与定位字段，并支持 severity/source/keyword 筛选。点击 object issue 选中节点并打开属性面板；点击 flow issue 选中连线。

`FieldError` 通过 `fieldPath` 匹配字段级错误；前端兼容 `memberChanges.0.valueExpression` 与 `memberChanges[0].valueExpression` 两种索引写法，但契约应保持一套稳定路径。FlowGram 只将带 `objectId` / `flowId` 的模型 issue 映射为节点/连线 error 或 warning；服务不可用这类全局 issue 不同步到所有节点。

## 回归清单

- 运行 `scripts/verify-microflow-validation-integration.ts` 检查 saved schema、inline schema、edit/save/publish/testRun、invalid schema、missing start、action fieldPath、metadata issue。
- 使用 `src/backend/Atlas.AppHost/Bosch.http/MicroflowBackend.http` 的 validation 段手工验证 envelope、summary、`serverValidatedAt`、resource not found 与 modeledOnly testRun。
- 断开后端时，ProblemPanel 显示“校验服务不可用”，save/publish/testRun 阻止继续，不 fallback local/mock validator。
