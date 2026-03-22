# PRD-Case: 低代码表单变量绑定引擎 - P0 阶段

## 1. 业务目标
为了让低代码表单页面 (基于 AMIS) 具备高级交互能力（如联动计算、动态验证、上下文注入），我们需要在原有静态 AMIS Schema 的基础上，引入**变量绑定机制**与**JS表达式沙盒引擎**。
本 P0 阶段旨在提供轻量级的变量作用域管理、基于 Mustache 语法的 `{{var}}` 插值解析、以及简单的沙盒安全执行层，使现有 `FormDesignerPage.vue` 不仅能拖拽界面，还能配置组件级变量和表达式。

## 2. 需求拆分 (前后端计划)

### 2.1 Frontend 实现计划
1. **表达式引擎层 (`ExpressionEngine.ts`)**:
   - 包含变量提取工具（解析 `{{xxx}}`）。
   - 实现轻量级的运算沙盒(`new Function` 配合代理 Proxy 拦截危险全局变量)，确保浏览器端运行环境代码安全。
2. **Schema 预处理器 (`AmisSchemaPreprocessor.ts`)**:
   - 在表单渲染投递给 AMIS 前，扫描整个 JSON Schema 树拦截自定义的变量配置(`$vars` / `expr`)，将其计算为原生属性或挂载到 AMIS context 数据域中。
3. **FormDesignerPage.vue 集成**:
   - 扩展 AMIS Editor 侧边栏，或单独增加“页面变量定义”面板（State Management）。
   - 确保可以声明页面级的初始变量。

### 2.2 Backend 实现计划
1. **统一验证接口预留**:
   - 如果未来要在提交时支持类似前端动态规则的计算，需要确保后端针对提交的 payload (如 DynamicRecordUpsertRequest) 有自定义扩展能力。
   - P0 重点在于前端的表达式闭环，后端只需确保现有的 FluentValidation 支持安全拦截异常。

## 3. 测试与验证标准
1. **页面配置**：可以在在 `FormDesignerPage` 定义页面级变量（例如 `currentUser`）。
2. **表达式渲染**：一个 TextField 组件的值如果被配置为 `{{ currentUser.name }}`，渲染模式应当将其转换为实际用户名。
3. **防逃逸测试**：在表达式沙盒中尝试运行 `window.location` 能够被 Proxy 拦截并抛出受限异常。
4. **0 Error / 0 Warning**：所有引入的新 TypeScript 文件与编译器校验均满足严格无报红要求。
