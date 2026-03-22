# PRD-Case: 数据库连接器高级预览与管理 - P0 阶段

## 1. 业务目标
针对现有 `TenantDataSourcesPage.vue` 仅能管理连接串和测试连通性的现状，本 P0 方向意图为平台带来能够执行**真实 SQL 或数据探查**的基础能力。
在低代码引擎及数据集成场景中，开发人员/实施人员需要通过安全通道执行查询，以确认数据格式或连接数据的状态。

## 2. 需求拆分 (前后端计划)

### 2.1 Backend 实现计划
1. **接口层扩展**：新增 `POST /api/v1/tenant-data-sources/{id}/query`，接收一个受控的 DTO (`SqlQueryRequest`) 包含 SQL 语句。
2. **防注入与权限控制 (`SqlOperationService`)**：
   - 限定此 P0 接口仅允许执行 `SELECT` 开头的语句，正则拦截任何包含 `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `TRUNCATE` 的破坏性关键词。
   - 使用现有数据源 ID 动态创建 `SqlSugarClient` 执行查询 (`Ado.SqlQuery<dynamic>`)。
   - 纳入平台统一 RBAC 拦截器，普通非管理员租户无法执行。

### 2.2 Frontend 实现计划
1. **统一的高级预览页 (`AdvancedDataPreviewPage.vue`)**:
   - 作为 `TenantDataSourcesPage` 的拓展。选定数据源后进入。
2. **轻量级 SQL 编辑器 (`SqlEditor.vue`)**:
   - P0 暂时利用 `textarea` 加简单等宽字体高亮样式构建，或者轻量引入代码高亮环境。支持一键发起查询。
3. **动态数据预览表 (`DataPreviewTable.vue`)**:
   - 接收后端动态返回的一维 JSON 数组，解析所有的 key 作为表头 (Columns)，渲染数据行。

## 3. 测试与验证标准
1. **注入拦截测试**：输入 `DELETE FROM xx`，API 需直接返回 HTTP 400 重大安全警报。
2. **安全隔离测试**：通过传入 `dataSourceId` 成功跨库查出对应表数据，不会影响系统默认库 `atlas.db`。
3. **结果渲染**：即使数据结构由于 `SELECT *` 不停变化，`DataPreviewTable` 也能自动推断列头并且展示。
