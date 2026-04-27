# Runtime Expression Contract v2

## 范围

本轮只定义 P0 可用表达式子集，不实现完整 Mendix 表达式语言，也不提供真实表达式执行器。前端表达式解析、类型推断与校验均基于 `MicroflowAuthoringSchema`、`MicroflowMetadataCatalog` 与 v2 `MicroflowVariableIndex`，禁止 `eval`、`Function` 或动态 JS 执行。

## 支持语法

- 变量：`$order`、`$currentUser`、`$currentIndex`、`$latestError`。
- 成员访问：`$order/Status`、`$order/Customer/Name`。第一版只精确推断第一层，多级 path 给 warning。
- 字面量：string、integer、decimal、boolean、null。
- 比较：`=`、`!=`、`>`、`<`、`>=`、`<=`。
- 逻辑：`and`、`or`、`not`。
- 算术：`+`、`-`、`*`、`/`，第一版仅做基础 number 推断。
- 函数：`empty()`、`toString()`；其他函数 warning。
- 条件：`if condition then expr else expr`。
- 枚举：`Module.Enum.Value`，或在 expected enumeration 上下文中使用 value。

## 输出模型

`parseExpression()` 返回 raw、AST、tokens、references 与 diagnostics。解析失败不会抛未捕获异常，而是返回 `unknown` AST 节点与 `MF_EXPR_PARSE_ERROR`。

`inferExpressionType()` 返回 inferredType、confidence、diagnostics、references。成员类型由 metadata 的 entity attribute / association 推导；变量类型由 VariableScopeEngine v2 查询得到。

`validateExpression()` 负责 required、变量作用域、成员访问、operator、function、if/then/else、expectedType 与 unknown type warning。

## Runtime 边界

Runtime DTO 原样携带 Authoring expression raw/text；后端执行器可按本契约实现 P0 子集。前端校验可信但不等同真实运行时执行。

## 第 51 轮后端 Runtime ExpressionEvaluator P0

- 后端新增 `IMicroflowExpressionEvaluator` / `MicroflowExpressionEvaluator`，链路为 raw expression -> tokenizer/parser -> AST -> type inference -> evaluation context -> VariableStore/Metadata -> evaluation result/runtime error。
- P0 支持变量 `$Name`、系统变量 `$currentUser/$currentIndex/$latestError/$latestHttpResponse`、对象成员 `$Order/Status`、string/integer/decimal/boolean/null/empty、comparison、`and/or/not`、`empty()`、`if then else`、枚举值和基础算术。
- 多层 member path 第一版只做受控解析；metadata 不足或 list<object> 直接成员访问会输出 warning/unknown，不伪装为高置信成功。
- 后端解释器禁止 eval、动态编译、任意脚本、业务数据库访问和外部 REST 调用；DateTime literal 本轮未实现专用 literal，按 unsupported/unknown 处理。
- `expectedType` 会在 Decision、ChangeVariable、End returnValue、Rest preview 等调用点生效；关键 mismatch 返回结构化 diagnostic/error。
- 不支持函数默认返回 `RUNTIME_EXPR_UNSUPPORTED_FUNCTION`；parse/member/type/divide-by-zero/unknown-variable 都保留 range、code、message。

## 第 52 轮 MetadataResolver 接入点

- `MicroflowExpressionEvaluationContext` 预留 `MetadataResolver` 与 `MetadataResolutionContext`；存在时 member access type inference 优先走 `ResolveMemberPath`，再回退旧 `MetadataCatalog` 逻辑。
- EnumerationValue 在提供 resolver context 时使用 `ResolveEnumerationValue` 校验并可使用 caption 作为 valuePreview。
- Runtime evaluation 对 resolver 返回的 missing member 生成结构化 diagnostic，不访问业务数据库，不执行真实 CRUD。
- list<object> 后续成员访问会保留 `LIST_TRAVERSAL_REQUIRES_LOOP` warning；第 54 轮 Object CRUD 与第 56 轮 CallMicroflow 可复用同一 resolver context。
