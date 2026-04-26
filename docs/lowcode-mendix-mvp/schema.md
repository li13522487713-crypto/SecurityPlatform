# Mendix MVP Schema

## 核心对象

- `LowCodeAppSchema`
- `ModuleSchema`
- `DomainModelSchema`
- `EntitySchema` / `AttributeSchema` / `AssociationSchema` / `EnumerationSchema`
- `PageSchema` / `WidgetSchema` / `WidgetBindingSchema` / `DataSourceSchema`
- `ExpressionSchema`
- `MicroflowSchema` / `MicroflowNodeSchema` / `MicroflowEdgeSchema`
- `WorkflowSchema` / `WorkflowNodeSchema` / `WorkflowEdgeSchema`
- `SecuritySchema`
- `RuntimePageModel` / `RuntimeWidgetSchema`
- `ExecuteActionRequest` / `ExecuteActionResponse` / `RuntimeUiCommand`
- `FlowExecutionTraceSchema`
- `ValidationErrorSchema`
- `SchemaMigrationPlan`
- `ExtensionManifestSchema`

## 类型特性

- 全局 `Ref<TKind>` 引用协议
- `DataTypeSchema` 支持 `Boolean/String/Integer/Long/Decimal/DateTime/Enumeration/Binary/Object/List/Nothing`
- `WidgetSchema`、`MicroflowNodeSchema`、`WorkflowNodeSchema` 使用 discriminated union
- `ExpressionSchema` 包含 `source + ast + returnType + dependencies + validation`

## 运行时校验

- `@atlas/mendix-schema` 提供 `LowCodeAppSchemaZod`
- 对关键对象执行 `zod.parse`，失败时在编辑器底部错误面板展示
- 通过 `isLowCodeAppSchema` 提供类型守卫
