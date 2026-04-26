# Mendix MVP Runtime

## Runtime Renderer 支持组件

- `container`
- `dataView`
- `textBox`
- `textArea`
- `numberInput`
- `dropDown`
- `button`
- `label`

## Action Executor 协议

- `executeAction(request)`
- 支持动作：
  - `callMicroflow`
  - `callWorkflow`
  - `showMessage`
- 产物：
  - `ExecuteActionResponse`
  - `RuntimeUiCommand[]`
  - `traceId`

## MF_SubmitPurchaseRequest 模拟逻辑

1. 读取 `Request.Amount`
2. `Amount <= 0` 返回 `validationFeedback`
3. `Amount > 50000` 设置 `Status = NeedFinanceApproval`
4. 否则设置 `Status = NeedManagerApproval`
5. 返回 `showMessage + refreshObject`
6. 生成 `FlowExecutionTraceSchema`

## Debug Trace

- 展示字段：
  - `traceId`
  - `flowType`
  - `flowId`
  - `startedAt/endedAt`
  - `status`
  - `inputArguments`
  - `steps[]`
- 每个 step 展示：
  - `nodeId/nodeType`
  - `expressionResults`
  - `permissionChecks`
  - `databaseQueries`
  - `uiCommands`
  - `inputSnapshot/outputSnapshot`
