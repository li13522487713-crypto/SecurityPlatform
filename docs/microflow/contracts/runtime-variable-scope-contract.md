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
