# Runtime 动作支持矩阵（P0 强类型）

| actionKind | 注册表 runtimeSupportLevel | ExecutionPlan（resolveActionRuntimeSupportLevel） | 说明 |
|------------|-----------------------------|-----------------------------------------------|------|
| retrieve, createObject, changeMembers, commit, delete, rollback, createVariable, changeVariable, callMicroflow, restCall, logMessage | **supported** | **supported** | 强类型 `Microflow*Action` + Runtime DTO 联合，不得使用 `MicroflowGenericAction` 表达 |
| 其它 P1/P2 | modeledOnly / beta / ... | 多为 modeledOnly 或按 availability 映射 | 走 `MicroflowGenericAction` 或专用接口；Runtime 为 modeledOnly/unsupported 策略 |

校验：`MF_ACTION_P0_MUST_BE_STRONGLY_TYPED` 在 P0 kind 但结构不合法时触发。

## 第 26 轮属性面板支持

| actionKind | 属性面板核心控件 | 输出变量 |
|------------|------------------|----------|
| retrieve | Entity/Association selector、ExpressionEditor、Sort、Range、ErrorHandlingEditor | `action.outputVariableName` |
| createObject | EntitySelector、MemberChange 表、commit 开关 | `action.outputVariableName` |
| changeMembers | VariableSelector、MemberChange 表、validateObject | 无 |
| commit / delete / rollback | object/list VariableSelector、事件/刷新/删除行为 | 无 |
| createVariable / changeVariable | VariableName/VariableSelector、DataTypeSelector、ExpressionEditor | createVariable 输出 `action.variableName` |
| callMicroflow | MicroflowSelector、参数 ExpressionEditor、return OutputVariableEditor | `action.returnValue.outputVariableName` |
| restCall | method/url/header/query/body/response/timeout | response/status/headers 变量 |
| logMessage | level、log node、template、arguments | 无 |
