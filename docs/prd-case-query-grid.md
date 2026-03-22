# PRD: Phase 1 - QueryGrid 一体化视图 (QueryGrid Unified View)

## 1. 业务目标
为了进一步提升低代码平台和应用侧的业务场景查询效率与展现协同性，我们需将现有的 `ProTable.vue` 和 `AdvancedQueryPanel.vue` (第一方向产出) 结合，打造统一的 `QueryGridUnifiedView.vue` 一体化视图。此组件作为业务层核心协同容器，将负责统一管理查询条件向后端发起请求，并将返回的数据投递到表格和分页控件。

## 2. 功能需求

### 2.1 顶层协同容器
- 构建 `QueryGridUnifiedView.vue`，作为页面级容器集成上述两个子组件。
- 它负责维护：
  - 高级查询条件状态 (AdvancedQueryCondition[])
  - 列表选择状态与选中行
  - 分页与排序状态

### 2.2 TableViewConfig 增强
- 后端需要允许前端持久化保存高级查询的配置或默认版式。
- 需要将这部分增强的状态定义融合进当前的 `TableViewConfig` 的 Schema 中。

### 2.3 ProTable / useCrudPage 升级
- 现有的 `ProTable.vue` 和 `useCrudPage.ts` 需要能支持接纳 `advancedFilters` 作为附带参数向外请求。
- 确保 `request` 时带上完整的查询树逻辑，并配合后台 `DynamicRecordQueryRequest` 工作。

## 3. 实现计划

### 3.1 Backend (后端)
1. **修改类型定义**: 更新 `docs/contracts.md` 及后端的 `TableViewConfig` 对象 (例如添加 `LayoutConfigs` 属性)。
2. **DTO 扩展**: 确保 `PagedRequest` 或是相关查询接口能优雅接收来自 QueryPanel 的结构化查询参数。

### 3.2 Frontend (前端)
1. **统一视图组件 (`QueryGridUnifiedView.vue`)**:
   - 组装 `<AdvancedQueryPanel />` 和 `<ProTable />`。
   - 使用 `@change` 事件从 Query Panel 拿取条件变化并直接触发 Table Reload。
2. **扩展 `useCrudPage.ts`**:
   - 添加入参或额外响应式引用用于管理和提交 `AdvancedQuery` 条件。
3. **接口对齐**:
   - 如果需要持久化 `TableViewConfig` 需要修改相应的类型及调用的 API。

## 4. 安全合规 (等保2.0)
- `TableViewConfig` 修改必须附带 `Idempotency-Key`。
- 新增的数据查询一律通过后端原有的 RBAC + 行级过滤器进行限制，不得越权。
