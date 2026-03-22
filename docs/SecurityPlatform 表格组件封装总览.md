# SecurityPlatform 表格组件封装总览

> **文档性质**：本文档是对 SecurityPlatform 代码库的深度分析报告，面向研发团队，提供前端表格组件的全面封装方案。
>
> **代码库**：lKGreat/SecurityPlatform（Vue 3.5 + TypeScript 5.9 + Ant Design Vue 4.2.6 + AMIS 6.0）
>
> **分析日期**：2026-03-23

---

## 一、现有表格能力现状速览

| 文件/模块 | 已实现能力 | 评估 |
|---|---|---|
| `composables/useTableView.ts` | 列配置（visible/order/width/pinned/align/ellipsis）、视图持久化（保存/另存/设默认/重置）、密度控制、合并单元格计算、400ms 防抖自动保存 | **核心骨架，质量较好** |
| `composables/useCrudPage.ts` | 分页、关键词搜索、权限控制（canCreate/canUpdate/canDelete）、集成 useTableView | **通用 CRUD 逻辑，可扩展** |
| `components/table/editable-cell.vue` | 单元格内联编辑（text/number/select）、ESC 取消、Enter 保存、异步回调 | **功能单薄，类型有限** |
| `components/table/table-view-toolbar.vue` | 视图切换下拉、保存/另存/设默认/重置、列设置面板、密度切换 | **UI 完整，缺少高级功能** |
| `components/crud/CrudPageLayout.vue` | 搜索栏、工具栏插槽、表格区域插槽、抽屉表单 | **布局框架，适用性强** |
| `services/api-system.ts` | 表格视图 CRUD API（/table-views）、设为默认、复制视图 | **后端支持完整** |
| `types/api.ts` | TableViewColumnConfig、TableViewConfig（含 sort/filters/groupBy/aggregations/queryModel/mergeCells）| **类型定义较完整** |
| `composables/useExcelExport.ts` | 用户模块专用导出/导入 | **仅限用户模块，未通用化** |
| `components/amis/` | AMIS 渲染器封装、AMIS 编辑器、业务插件注册 | **低代码集成基础具备** |

---

## 二、12 个维度能力缺口总览

| 维度 | 名称 | 现状 | 优先级 | 预估人天 |
|:---:|---|:---:|:---:|:---:|
| 1 | 表头与布局能力 | 部分 | **P0** | 25 |
| 2 | 列管理能力 | 部分 | **P0** | 15 |
| 3 | 数据查看能力 | 部分 | **P0** | 20 |
| 4 | 行操作与选择能力 | 缺失 | **P0** | 15 |
| 5 | 编辑能力 | 部分 | P1 | 20 |
| 6 | 性能能力 | 缺失 | P1 | 15 |
| 7 | 交互体验能力 | 部分 | P1 | 12 |
| 8 | 权限与配置能力 | 部分 | **P0** | 15 |
| 9 | 导入导出能力 | 部分 | P1 | 12 |
| 10 | 国际化与可访问性 | 部分 | P1 | 10 |
| 11 | 状态持久化能力 | 部分 | **P0** | 10 |
| 12 | 业务高级能力 | 缺失 | P2 | 30 |
| **合计** | | | | **~199 人天** |

---

## 三、核心封装架构设计

### 3.1 组件层次结构

```
ProTable（核心高级表格组件）
├── ProTableHeader（复杂表头渲染）
├── ProTableBody（虚拟滚动 + 单元格渲染）
├── ProTableFooter（汇总行 + 分页）
├── ProTableToolbar（工具栏）
│   ├── TableViewToolbar（视图管理，已有）
│   ├── ColumnSettingPanel（列设置面板，已有，需增强）
│   ├── FilterPanel（高级筛选面板，待建）
│   └── BatchActionBar（批量操作栏，待建）
└── ProTableEmpty（空态/加载态）
```

### 3.2 统一列状态模型（ColumnStateModel）

```typescript
interface ColumnState {
  key: string           // 列唯一标识
  visible: boolean      // 是否可见
  fixed: 'left' | 'right' | false  // 冻结方向
  width: number         // 列宽（px）
  order: number         // 排列顺序
  sort?: 'asc' | 'desc' | null     // 排序状态
  filter?: FilterValue  // 筛选值
  permission?: {        // 权限控制
    canView: boolean
    canEdit: boolean
  }
}
```

### 3.3 视图持久化扩展

现有 `TableViewConfig` 已具备良好基础，需在以下方向扩展：

```typescript
interface TableViewConfig {
  // 已有字段（保持不变）
  columns: TableViewColumnConfig[]
  density: 'compact' | 'default' | 'comfortable'
  pagination: { pageSize: number }
  sort: TableViewSort[]
  filters: TableViewFilter[]
  
  // 新增扩展字段
  complexHeaders?: ComplexHeaderConfig[]  // 多级表头配置
  rowSelection?: RowSelectionConfig       // 行选择配置
  virtualScroll?: VirtualScrollConfig     // 虚拟滚动配置
  editMode?: 'none' | 'cell' | 'row'     // 编辑模式
  conditionalFormats?: ConditionalFormat[] // 条件格式规则
  urlSync?: boolean                        // URL 参数同步
}
```

---

## 四、双场景集成策略

### 4.1 普通 Vue 面板场景

```vue
<!-- 使用示例 -->
<ProTable
  table-key="system-users"
  :columns="columns"
  :data-source="dataSource"
  :loading="loading"
  :pagination="pagination"
  :row-selection="{ type: 'checkbox' }"
  :complex-headers="complexHeaders"
  :virtual-scroll="{ enabled: true, rowHeight: 48 }"
  @change="onTableChange"
  @batch-action="onBatchAction"
/>
```

### 4.2 低代码 AMIS 场景

```json
{
  "type": "atlas-pro-table",
  "tableKey": "dynamic-table-key",
  "api": "/api/v1/dynamic-tables/{key}/records",
  "columns": [...],
  "features": {
    "complexHeaders": true,
    "virtualScroll": true,
    "viewPersistence": true,
    "columnPermission": true,
    "batchActions": [
      { "label": "批量删除", "action": "delete" }
    ]
  }
}
```

---

## 五、实施路线图（分三期）

### 第一期：基础版（P0，约 65 人天）

| 优先级 | 功能 | 说明 |
|---|---|---|
| P0 | 复杂表头 | 多级表头、分组列、colspan/rowspan |
| P0 | 冻结列/固定表头 | 完整实现 scroll + fixed |
| P0 | 列宽拖拽 | 拖拽调整宽度并持久化 |
| P0 | 行选择与批量操作 | 单选/多选/全选/批量操作栏 |
| P0 | 高级筛选面板 | 条件构建器（AND/OR/嵌套） |
| P0 | 列权限控制 | 按角色控制列可见性 |
| P0 | 视图持久化增强 | URL 同步、视图分享链接 |

### 第二期：进阶版（P1，约 74 人天）

| 优先级 | 功能 | 说明 |
|---|---|---|
| P1 | 可编辑表格 | 行/单元格编辑、校验、撤销 |
| P1 | 虚拟滚动 | 行虚拟化（vue-virtual-scroller） |
| P1 | 树形表格 | 展开/折叠、懒加载子节点 |
| P1 | 导入导出通用化 | 通用 Excel 导入导出封装 |
| P1 | 国际化完善 | 表头多语言、数字/日期本地化 |
| P1 | 无障碍访问 | aria 属性、键盘导航 |

### 第三期：高阶版（P2，约 30 人天）

| 优先级 | 功能 | 说明 |
|---|---|---|
| P2 | 树表/主子表 | 主表-子表联动 |
| P2 | 行分组聚合 | 按列分组 + sum/count/avg |
| P2 | 条件格式规则引擎 | 规则配置 + 单元格高亮 |
| P2 | 可配置计算列 | 公式计算列 |

---

## 六、关键技术决策

| 决策点 | 推荐方案 | 理由 |
|---|---|---|
| 虚拟滚动库 | `vue-virtual-scroller` | Vue 3 原生支持，社区活跃 |
| 复杂表头实现 | 扩展 Ant Design Vue `a-table` columns 嵌套 | 原生支持，无需额外库 |
| 列宽拖拽 | 自定义 `ResizableHeader` 组件 | 与现有 useTableView 深度集成 |
| 高级筛选 | 基于现有 `TableViewQueryGroup` 类型构建 UI | 类型定义已完整，只缺 UI |
| AMIS 集成 | 注册 `atlas-pro-table` 自定义插件 | 复用 Vue 组件，避免重复开发 |
| 视图持久化 | 扩展现有 `/api/v1/table-views` API | 后端已完整实现，只需前端扩展 |
| 权限控制 | 集成 `/dynamic-tables/{key}/field-permissions` | 后端 API 已支持 |

---

## 七、等保2.0 合规要求

由于本项目是等保2.0三级合规平台，表格组件封装需特别注意：

- **敏感字段脱敏**：手机号、证件号等敏感列需在前端渲染层进行脱敏显示，且即使用户在列配置中勾选，也需通过权限 API 校验后才能展示原文。
- **操作审计**：批量删除、批量编辑等高危操作需触发审计事件（`TABLE_BATCH_OPERATE`），记录操作人、操作时间、影响行数。
- **导出审计**：导出操作需记录审计日志（`TABLE_EXPORT`），包含导出条件、导出行数、导出时间。
- **幂等+CSRF**：所有写操作（批量编辑、导入）必须携带 `Idempotency-Key` 和 `X-CSRF-TOKEN`，复用现有 `api-core.ts` 的安全机制。
