# PRD-Case: 高级查询面板 (AdvancedQueryPanel) - P0 MVP 阶段

## 1. 业务目标
为了替换现有只支持无层次单层列表（`filters` 数组）的基础过滤，我们需要提供一个 **可视化的复杂查询条件构建器**。
通过本组件，用户可以在前端拖拽和配置多层级嵌套的 `AND/OR` 条件分支，并下发给后端产生动态且安全的 SQL WHERE 语句。它是“数据网格一体化 (QueryGrid)”和“自动业务场景搭建”的核心底座。

## 2. 需求拆分 (前端与后端计划)

### 2.1 后端实现计划
1. **DTO 数据约束扩展**：
   - 修改 `DynamicRecordQueryRequest.cs`（如果存在于后端）或其对应接收分页与查询的 DTO，加入 `AdvancedQueryConfig` 树状结构。
   - 保留原有的直接搜索 (`keyword`) 为兼容处理。
2. **复杂条件树解析与 SQL 构建（SqlSugar）**：
   - 核心难点：将 `QueryGroup` (AND/OR) 以及内部的 `QueryRule` (字段, 操作符, 值) 安全地解析为 SqlSugar 的条件表达式（或原生的参数化 SQL 字符串）。
   - 防止 SQL 注入：在拼接表名、字段名时必须使用白名单或系统元数据(`DynamicTable` schema) 校验其合法性，针对 `value` 必须使用参数化传递（如 `@p1`, `@p2`）。
3. **接口测试**：
   - 更新 `Bosch.http/DynamicTables.http`（或其他模块对应的查询 `.http` 文件），加入包含嵌套深度 `>= 2` 层的 JSON payload 来验证逻辑正确性与安全拦截。

### 2.2 前端实现计划
1. **TypeScript 强类型定义** (`src/types/advanced-query.ts`)：
   - 定义 `QueryRule`, `QueryGroup`, `QueryOperator` (枚举，如 `=`, `!=`, `>`, `LIKE`, `IN` 等), 以及 `AdvancedQueryConfig`。
2. **核心构建器组件 (`QueryConditionBuilder.vue`)**：
   - 设计为支持无限递归的 Vue 组件。
   - `QueryGroup.vue`: 渲染外层框线和逻辑切换操作符 (`AND`/`OR`)，并提供添加子规则或子分组的按钮。
   - `QueryRule.vue`: 每行展示三元组（字段下拉框、操作符下拉框、值输入框）。需要针对字段的类型（字符串、数字、日期、布尔）动态切换前端表单控件 (`a-input`, `a-input-number`, `a-date-picker`, `a-switch`)。
3. **顶层容器组件 (`AdvancedQueryPanel.vue`)**：
   - 聚合暴露：对外暴露出 `v-model:query-config` 和 `@on-query` 事件，供页面上的 Table 直接调用取参。

## 3. 测试与验证标准 (验收)
1. **前端静态验证**：TypeScript 编译零错误 (`npm run build` 和 `npm run lint` 通过)。确保没有 `any` 的滥用。
2. **后端静态验证**：.NET 编译 0 Error, 0 Warning。
3. **功能端到端验证**：
   - 在前端点击构造能够表示 `(Status = 1 OR Type = 'Admin') AND CreatedAt > '2026-01-01'` 的对象树。
   - 请求发往后端，后端能够准确返回命中的数据（手工在 Sqlite 查看日志或数据对比）。
   - 不同用户的请求依然受到等保 2.0 中的数据权限和角色边界隔离（即查询不能越权）。
