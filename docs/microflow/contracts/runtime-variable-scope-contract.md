# Runtime Variable Scope Contract v2

## 权威模型

- `MicroflowAuthoringSchema` 是唯一业务主模型。
- `MicroflowVariableIndex` 由 AuthoringSchema + `MicroflowMetadataCatalog` 静态分析生成，不能作为业务主存储。
- 变量系统不依赖 FlowGram JSON，不直接 import mock metadata；调用方必须显式传入 MetadataCatalog。

## 可见性

- `definite`：所有到达当前对象的正常执行路径均已声明该变量。
- `maybe`：至少一条可达路径声明变量，但不是所有路径都声明。典型场景是 Decision 分支变量在 Merge 后使用。
- `unavailable`：当前对象前不可见；Selector 默认不显示，Validator 报错。

## 作用域

- `global`：参数与 `$currentUser`，所有对象可见。
- `downstream`：普通 action 输出，仅在声明节点之后的 normal downstream path 可见。
- `branch`：Decision 分支语义由 normal flow + 近似支配关系推导，分支内变量不跨到兄弟分支。
- `loop`：iterator 与 `$currentIndex` 只在 Loop nested collection 内可见；Loop 内声明变量不流出 Loop。
- `errorHandler`：`$latestError`、`$latestHttpResponse`、`$latestSoapFault` 只在对应 error handler flow 目标及其错误处理下游可见。

## P0 输出类型

- Retrieve database `all/custom` 为 `list<object>`，`first` 为 `object`。
- Retrieve association 按 association multiplicity 推导 `object` 或 `list<object>`。
- CreateObject 输出 `object entityQualifiedName`。
- CreateVariable 输出 action.dataType。
- CallMicroflow 从 MetadataCatalog 的 target returnType 推导；void 不声明变量并给 warning。
- RestCall response string/json/statusCode/headers 分别输出 string/json/integer/json；importMapping 为 unknown warning。
- Generic/modeledOnly action 输出只作为 modeledOnly/unknown，并给 warning。

## Validator

变量不存在、不可见、maybe、重复名、非法名、类型不匹配、readonly/system 修改、metadata 缺失与 modeledOnly unknown 均必须形成 `MicroflowValidationIssue` 或 VariableIndex diagnostic，并带准确 `fieldPath`。

## 第 50 轮 Runtime VariableStore

- 后端新增 `IMicroflowVariableStore` / `MicroflowVariableStore`，只负责运行时变量 define/get/set/remove、作用域栈、快照与结构化诊断。
- VariableStore 只消费 `MicroflowExecutionPlan` 与 `RuntimeExecutionContext`，不依赖 FlowGram JSON，不修改 AuthoringSchema，不访问业务数据库，不调用外部 REST，不执行表达式。
- 运行时变量值包含 `dataTypeJson`、`kind`、`rawValueJson`、`valuePreview`、`sourceKind`、source object/action、collection/loop、readonly/system 与时间戳。
- ScopeStack 支持 global/action/loop/errorHandler/call/system；Loop iteration push 后定义 iterator 与 `$currentIndex`，pop 后不可见；ErrorHandler push 后定义 `$latestError` 与 REST 错误下的 `$latestHttpResponse`，pop 后不可见。
- `$currentUser` 在 system/global scope 初始化，readonly 且 system；普通 action 或 ChangeVariable 不得改写。
- Snapshot 面向 `TraceFrame.variablesSnapshot`，默认包含安全 `valuePreview`，可按 option 省略 raw value；当前仅做基础脱敏说明，完整敏感字段策略留后续轮次。

## 第 51 轮 ExpressionEvaluator 读取约定

- ExpressionEvaluator 只通过 `RuntimeExecutionContext.VariableStore` 读取变量，不绕过作用域栈，也不修改变量声明模型。
- 变量引用 `$Name` 会按 `Name` 与 `$Name` 两种运行时存储形式查找，以兼容参数变量和系统变量。
- `$currentIndex` 仅在 loop scope 内由 VariableStore 暴露；`$latestError/$latestHttpResponse` 仅在 error handler scope 内暴露，离开 scope 后表达式会得到 `RUNTIME_VARIABLE_NOT_FOUND`。
- CreateVariable/ChangeVariable 的写入仍由 VariableStore 拦截 readonly/system 变量；ExpressionEvaluator 只产出可序列化 value/result。
