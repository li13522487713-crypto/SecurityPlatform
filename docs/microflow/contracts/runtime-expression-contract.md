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
