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

---

# 各维度详细封装方案


> 本方案基于对 SecurityPlatform 代码库（Vue 3 + TypeScript + Ant Design Vue 4.x + AMIS 低代码）的深度阅读，通过并行分析 12 个核心能力维度，给出完整的组件封装方案。重点涵盖普通 Vue 3 面板与低代码 AMIS 场景，以及如何将配置持久化到用户登录账户中。

## 目录

- 维度 1：表头与布局能力
- 维度 2：列管理能力
- 维度 3：数据查看能力
- 维度 4：行操作与选择能力
- 维度 5：编辑能力
- 维度 6：性能能力
- 维度 7：交互体验能力
- 维度 8：权限与配置能力
- 维度 9：导入导出能力
- 维度 10：国际化与可访问性
- 维度 11：状态持久化能力
- 维度 12：业务高级能力

---

## 维度 1：表头与布局能力

- **现状评估**：部分
- **实施优先级**：P0
- **预估人天**：25 天

### 一、现状分析

#### 1.1 代码库中已有哪些相关实现

根据代码库摘要，现有实现与表头布局能力相关的部分主要分散在 `composables/useTableView.ts` 和 `table-view-toolbar.vue` 中：

- **`composables/useTableView.ts`**: 
  - **列配置管理**: 支持列的 `visible` (显示/隐藏), `order` (顺序), `width` (宽度), `pinned` (固定), `align` (对齐), `ellipsis` (省略) 等基础配置。这是布局能力的核心数据管理部分。
  - **密度控制**: 通过 `compact/default/comfortable` 选项，间接控制行高。
  - **合并单元格**: 提供了 `computeMergeSpans/getMergeCells` 方法，具备了数据行单元格合并的计算能力。

- **`components/table/table-view-toolbar.vue`**:
  - **列设置面板**: 提供了 UI 界面，让用户可以操作列的显示/隐藏、上移/下移、固定到左侧/右侧。

- **`types/api.ts`**:
  - **`TableViewColumnConfig`**: 定义了列配置的核心属性，如 `key`, `visible`, `order`, `width`, `pinned`, `align`, `ellipsis`。
  - **`TableViewConfig`**: 定义了整体视图配置，包含 `columns`, `density`, `mergeCells` 等。

#### 1.2 存在哪些缺口和不足

尽管具备了基础的列配置能力，但距离需求清单中的高级布局功能仍有显著差距：

- **复杂表头**: 完全缺失。不支持多级表头、分组列、以及表头的 `colspan` / `rowSpan`。
- **动态与自适应布局**:
  - **动态表头**: 不支持根据数据动态生成或改变表头结构。
  - **列宽拖拽**: 未实现。用户无法通过拖拽交互式地调整列宽。
  - **自适应列宽**: 未实现。表格列宽不能根据容器宽度自适应调整。
- **滚动与冻结**: 
  - **固定表头/冻结列**: 虽然 `pinned` 属性存在，但摘要中未明确说明其在 `a-table` 中的完整实现情况，特别是与横向滚动的配合。
- **样式与视觉**: 
  - **斑马纹/边框/紧凑模式**: 仅有 `density` 间接控制紧凑度，缺乏对斑马纹和边框的显式配置能力。

### 二、封装目标

#### 2.1 该维度需要封装哪些核心能力

针对现状缺口和用户需求，本维度的封装目标是构建一个功能完备、配置灵活的表头与布局解决方案。

| 核心能力 | 封装说明 |
| :--- | :--- |
| **多级/复杂表头** | 支持通过嵌套的 `children` 属性定义无限层级的表头，自动处理 `colspan` 和 `rowspan`。 |
| **分组列** | 作为多级表头的一种典型应用场景，允许将相关列组织在同一个分组下。 |
| **动态表头** | 允许 `columns` 配置是动态响应式的，当配置变更时，表格能够重新渲染。 |
| **固定表头/冻结列** | 完整实现 Ant Design Vue 的 `fixed` 和 `scroll` 配置，确保在出现横向或纵向滚动条时，表头和指定列能够固定。 |
| **横纵向滚动** | 支持通过 `scroll.x` 和 `scroll.y` 配置表格的滚动行为。 |
| **列宽拖拽** | 实现通过拖拽表头分隔线来动态调整列宽，并将新宽度持久化。 |
| **自适应列宽** | 提供一种机制，使得表格列宽能根据容器宽度变化而自动调整，避免出现不必要的横向滚动条。 |
| **单元格省略 tooltip** | 封装 `ellipsis` 和 `tooltip` 行为，当单元格内容被截断时，自动在鼠标悬浮时显示完整内容的 Tooltip。 |
| **行高/紧凑模式** | 将 `density` 配置（紧凑/默认/舒适）与 Ant Design Vue `a-table` 的 `size` 属性关联，以控制行高。 |
| **合并单元格** | 将现有的 `computeMergeSpans` 逻辑与封装后的表格组件集成，支持动态计算数据行的 `rowspan`。 |
| **斑马纹/边框** | 提供 `stripe` 和 `bordered` 配置项，允许用户开启或关闭表格的斑马纹和边框样式。 |

#### 2.2 普通面板场景 vs 低代码场景的差异处理

- **普通面板场景 (Vue Component)**: 
  - **实现重点**: 打造一个名为 `ProTable.vue` 的高级表格组件，它深度整合 `useTableView.ts`，接收 `TableViewConfig` 作为核心 `prop`，并负责将所有配置项转换为 Ant Design Vue `a-table` 的 `props` 和 `slots`。所有交互（如列宽拖拽、列设置）都在 Vue 组件内部完成，并通过 `emits` 与 `useTableView` 通信以实现状态持久化。
  - **用户体验**: 提供最完整、最流畅的交互体验，例如平滑的列宽拖拽、功能丰富的列设置面板等。

- **低代码场景 (AMIS)**:
  - **实现重点**: 核心目标是将 `TableViewConfig` 的能力**映射**到 AMIS 的 `table` 组件 `schema` 上。尽可能利用 AMIS `table` 的原生能力（如 `columns` 的嵌套、`fixed`、`resizable` 等）。
  - **差异处理**: 对于 AMIS 原生不支持或体验不佳的功能（例如，与 `useTableView` 深度集成的列设置面板），则需要考虑注册自定义 AMIS 插件。该插件可以是一个封装了 `ProTable.vue` 的 React 组件，从而在 AMIS 中复用 Vue 的高级表格能力。这种方式需要处理好 Vue 和 React 的混用、通信和状态同步问题。

### 三、核心数据模型 / 类型定义

#### 3.1 给出 TypeScript 接口/类型定义

为了支持“表头与布局能力”维度下的各项功能，我们需要对现有的 `TableViewColumnConfig` 和 `TableViewConfig` 进行扩展。以下是建议的 TypeScript 接口定义：

```typescript
// types/api.ts 或 types/table.ts (新增)

/**
 * 表格列配置项
 * 扩展 Ant Design Vue TableColumnType
 */
export interface TableViewColumnConfig {
  key: string; // 唯一标识
  dataIndex?: string; // 对应数据源的字段名
  title?: string; // 列头显示名称
  visible?: boolean; // 是否显示该列
  order?: number; // 列的显示顺序
  width?: number | string; // 列宽，支持像素或百分比
  minWidth?: number; // 最小列宽，用于列宽拖拽和自适应
  maxWidth?: number; // 最大列宽，用于列宽拖拽和自适应
  fixed?: 'left' | 'right' | false; // 固定列方向
  align?: 'left' | 'center' | 'right'; // 列内容对齐方式
  ellipsis?: boolean; // 内容超出是否省略
  tooltip?: boolean; // 省略时是否显示 tooltip
  resizable?: boolean; // 是否允许拖拽调整列宽
  colSpan?: number; // 表头单元格横向合并格数 (用于复杂表头)
  rowSpan?: number; // 表头单元格纵向合并格数 (用于复杂表头)
  children?: TableViewColumnConfig[]; // 子列，用于多级表头和分组列
  // ... 其他现有属性
}

/**
 * 表格视图配置
 * 扩展现有 TableViewConfig
 */
export interface TableViewConfig {
  columns: TableViewColumnConfig[]; // 列配置数组
  density?: 'compact' | 'default' | 'comfortable'; // 表格密度，影响行高
  bordered?: boolean; // 是否显示表格边框
  stripe?: boolean; // 是否显示斑马纹
  scroll?: {
    x?: number | string; // 横向滚动区域宽度，支持像素或 'max-content'
    y?: number | string; // 纵向滚动区域高度
  }; 
  mergeCells?: MergeCellRule[]; // 单元格合并规则 (现有)
  // ... 其他现有属性，如 pagination, sort, filters, groupBy, aggregations, queryPanel, queryModel
}

// 现有 MergeCellRule 保持不变
// export interface MergeCellRule {
//   columnKey: string;
//   dependsOn?: string[];
// }
```

#### 3.2 说明与现有 TableViewConfig 的集成方式

上述类型定义是对现有 `types/api.ts` 中 `TableViewColumnConfig` 和 `TableViewConfig` 的**直接扩展**。具体集成方式如下：

1.  **`TableViewColumnConfig` 扩展**：
    *   新增 `minWidth`, `maxWidth`, `resizable`, `colSpan`, `rowSpan`, `children` 等属性。这些属性将直接添加到 `TableViewColumnConfig` 接口中，使其能够描述更复杂的列结构和行为。
    *   `children` 属性是实现多级表头和分组列的关键，它允许列配置以树形结构组织。
    *   `colSpan` 和 `rowSpan` 主要用于描述表头单元格的合并情况，尽管 Ant Design Vue 的 `columns` 配置通常通过 `children` 结构隐式处理表头合并，但显式定义这些属性有助于更灵活地构建复杂表头。

2.  **`TableViewConfig` 扩展**：
    *   新增 `bordered`, `stripe`, `scroll` 等属性。这些属性将直接添加到 `TableViewConfig` 接口中，用于控制表格的整体布局和样式。
    *   `scroll` 对象用于配置表格的横向和纵向滚动，支持固定表头和冻结列的场景。
    *   `density` 属性已存在于 `useTableView.ts` 中，但确保其在 `TableViewConfig` 中有明确的类型定义，并与 `a-table` 的 `size` 属性对应，以控制行高和紧凑模式。

3.  **`useTableView.ts` 集成**：
    *   `useTableView` composable 需要更新其内部状态管理逻辑，以支持这些新增的配置属性。
    *   在 `saveView`, `updateTableViewConfig` 等方法中，需要确保能够正确地序列化和反序列化包含这些新属性的 `TableViewConfig`。
    *   在 `ColumnSettingItem` 类型中，可能需要增加对 `minWidth`, `maxWidth`, `resizable`, `children` 等属性的映射，以便在列设置面板中进行配置。

4.  **API 接口集成**：
    *   后端 `TableViewConfig` 的存储和读取 API (`services/api-system.ts` 中的 `createTableView`, `updateTableView`, `updateTableViewConfig` 等) 需要能够支持这些扩展后的 `TableViewConfig` 结构。这意味着数据库模型或存储格式需要能够容纳这些新增的字段，特别是 `columns` 字段可能需要存储更复杂的 JSON 结构。

通过以上扩展，我们可以将“表头与布局能力”维度的所有配置项统一纳入到 `TableViewConfig` 中，实现配置的集中管理和持久化。

### 四、组件/Composable 封装方案

#### 4.1 新增或改造哪些文件

为了实现“表头与布局能力”维度的封装，以下文件将进行新增或改造：

1.  **`composables/useTableView.ts` (改造)**：
    *   **功能**：作为表格视图管理的核心 Composable，需要扩展其内部状态和方法，以支持对 `TableViewConfig` 中新增的表头与布局相关属性（如 `scroll`, `bordered`, `stripe`, `density` 以及 `TableViewColumnConfig` 中的 `fixed`, `width`, `resizable`, `children` 等）的读取、更新和持久化。
    *   **改造点**：
        *   更新 `TableViewState` 类型以包含新的配置项。
        *   修改 `saveView`, `updateTableViewConfig` 等方法，确保能正确处理和存储扩展后的 `TableViewConfig`。
        *   引入列宽拖拽后的 `width` 更新逻辑，并触发自动保存。
        *   可能需要增加处理多级表头结构的方法，例如在列设置面板中进行列的拖拽排序和可见性切换时，需要递归处理 `children` 属性。

2.  **`components/table/ProTable.vue` (新增或改造)**：
    *   **功能**：建议新增一个 `ProTable.vue` 组件，作为 Ant Design Vue `a-table` 的高级封装。它将接收 `TableViewConfig` 作为 `prop`，并负责将配置映射到 `a-table` 的相应 `props` 和 `slots`，实现多级表头、固定列、横纵向滚动、列宽拖拽、单元格省略 tooltip、行高、斑马纹、边框等功能。
    *   **路径建议**：`components/table/ProTable.vue`。
    *   **改造点**：如果已有类似的表格封装组件，则在其基础上进行改造。

3.  **`components/table/table-view-toolbar.vue` (改造)**：
    *   **功能**：该工具栏组件包含列设置面板。需要改造其 UI 和逻辑，以支持对 `TableViewColumnConfig` 中新增属性的配置，例如：
        *   允许用户配置列的 `fixed` 状态（左固定/右固定/不固定）。
        *   显示和编辑列的 `width`。
        *   支持多级表头的层级展示和操作（例如，可以隐藏整个列组）。
        *   密度切换菜单已存在，但需确保其与 `TableViewConfig.density` 的双向绑定。

4.  **`types/api.ts` 或 `types/table.ts` (改造)**：
    *   **功能**：如前所述，扩展 `TableViewColumnConfig` 和 `TableViewConfig` 接口，以包含所有表头与布局相关的配置属性。

#### 4.2 关键 Props/Emits/Expose 设计

**`components/table/ProTable.vue`**

```typescript
// ProTable.vue
<script setup lang="ts">
import { TableViewConfig, TableViewColumnConfig, MergeCellRule } from "@/types/api"; // 假设类型定义在 @/types/api
import { TableProps } from "ant-design-vue";
import { computed } from "vue";

interface Props {
  config: TableViewConfig; // 表格视图配置
  dataSource: any[]; // 表格数据源
  loading?: boolean; // 加载状态
  pagination?: TableProps["pagination"]; // 分页配置
  mergeCells?: MergeCellRule[]; // 单元格合并规则
  // ... 其他 Ant Design Vue a-table 原生 props
}

const props = defineProps<Props>();
const emit = defineEmits<{ 
  (e: "update:config", config: TableViewConfig): void; // 配置更新事件，例如列宽拖拽后
  (e: "change", ...args: Parameters<TableProps["onChange"]>): void; // 表格标准 change 事件
}>();

// Expose 示例，用于外部调用表格内部方法
defineExpose({
  scrollTo: (options: { x?: number | string; y?: number | string }) => { /* ... */ },
  // ... 其他需要暴露的方法
});

// 辅助函数：将内部 TableViewColumnConfig 结构转换为 Ant Design Vue 兼容的 columns 结构
const mapColumnsToAntd = (columns: TableViewColumnConfig[]): TableProps["columns"] => {
  return columns.map(col => {
    const antdCol: any = {
      ...col,
      fixed: col.fixed === false ? undefined : col.fixed, // Ant Design Vue fixed: false 时应为 undefined
      children: col.children ? mapColumnsToAntd(col.children) : undefined,
    };
    // 处理 colSpan 和 rowSpan，Ant Design Vue 通常通过 children 结构隐式处理表头合并
    // 如果需要显式控制，可能需要 customHeaderCell
    if (col.colSpan) antdCol.colSpan = col.colSpan;
    if (col.rowSpan) antdCol.rowSpan = col.rowSpan;
    return antdCol;
  });
};

// 内部计算属性或方法，用于将 config 映射到 a-table props
const antTableProps = computed(() => ({
  columns: mapColumnsToAntd(props.config.columns), // 映射为 Ant Design Vue 兼容的 columns 结构
  scroll: props.config.scroll,
  bordered: props.config.bordered,
  size: props.config.density, // 映射 density 到 size
  // ... 其他映射
}));

// 辅助函数：根据 key 查找列配置
const findColumnByKey = (columns: TableViewColumnConfig[], key: string): TableViewColumnConfig | undefined => {
  for (const col of columns) {
    if (col.key === key) return col;
    if (col.children) {
      const found = findColumnByKey(col.children, key);
      if (found) return found;
    }
  }
  return undefined;
};

// 列宽拖拽相关逻辑
let startX = 0;
let startWidth = 0;
let resizingColumnKey: string | null = null;

const startResize = (key: string, event: MouseEvent) => {
  resizingColumnKey = key;
  startX = event.clientX;
  const column = findColumnByKey(props.config.columns, key);
  if (column && column.width) {
    startWidth = typeof column.width === 'number' ? column.width : parseFloat(column.width as string);
  } else {
    // 如果没有预设宽度，可以尝试获取当前渲染宽度
    const headerCell = (event.target as HTMLElement).closest('th');
    startWidth = headerCell ? headerCell.offsetWidth : 100; // 默认值
  }
  document.addEventListener('mousemove', doResize);
  document.addEventListener('mouseup', stopResize);
};

const doResize = (event: MouseEvent) => {
  if (!resizingColumnKey) return;
  const deltaX = event.clientX - startX;
  let newWidth = Math.max(startWidth + deltaX, 50); // 最小宽度限制

  const newConfig = { ...props.config };
  const column = findColumnByKey(newConfig.columns, resizingColumnKey);
  if (column) {
    if (column.minWidth && newWidth < column.minWidth) newWidth = column.minWidth;
    if (column.maxWidth && newWidth > column.maxWidth) newWidth = column.maxWidth;
    column.width = newWidth;
    emit('update:config', newConfig); // 实时更新配置，触发视图更新
  }
};

const stopResize = () => {
  resizingColumnKey = null;
  document.removeEventListener('mousemove', doResize);
  document.removeEventListener('mouseup', stopResize);
};

// 单元格合并逻辑 (需要结合 useTableView 中的 computeMergeSpans)
const onCell = (record: any, rowIndex: number) => {
  // 假设 useTableView 提供了 getMergeCells 方法
  // const mergeCells = useTableView.getMergeCells(props.dataSource, props.mergeCells);
  // return mergeCells[rowIndex]; // 返回 { rowspan, colspan } 结构
  return {}; // 占位，实际需要集成 useTableView 的合并逻辑
};

</script>

<template>
  <a-table
    v-bind="antTableProps"
    :data-source="dataSource"
    :loading="loading"
    :pagination="pagination"
    @change="emit(\"change\", ...$event)"
    :custom-cell="onCell" <!-- 单元格合并 -->
  >
    <!-- 自定义表头单元格，用于列宽拖拽 -->
    <template #headerCell="{ column }">
      <div class="resizable-header-cell">
        {{ column.title }}
        <span v-if="column.resizable" class="column-resize-handle" @mousedown="startResize(column.key, $event)"></span>
      </div>
    </template>
    <!-- 自定义单元格，用于合并单元格和 tooltip -->
    <template #bodyCell="{ column, record, index }">
      <template v-if="column.ellipsis && column.tooltip">
        <a-tooltip :title="record[column.dataIndex]">
          <span class="ellipsis-text">{{ record[column.dataIndex] }}</span>
        </a-tooltip>
      </template>
      <template v-else>
        <slot :name="`bodyCell-${column.key}`" :column="column" :record="record" :index="index">
          {{ record[column.dataIndex] }}
        </slot>
      </template>
    </template>
  </a-table>
</template>

<style scoped>
.resizable-header-cell {
  position: relative;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-right: 8px; /* 为拖拽手柄留出空间 */
}
.column-resize-handle {
  position: absolute;
  right: 0; /* 拖拽手柄位置 */
  top: 0;
  bottom: 0;
  width: 8px; /* 拖拽手柄宽度 */
  cursor: col-resize;
  z-index: 1;
}
.ellipsis-text {
  display: block;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
}
</style>
```

**`components/table/table-view-toolbar.vue`**

```typescript
// table-view-toolbar.vue
<script setup lang="ts">
import { TableViewColumnConfig, TableViewDensity } from "@/types/api";
import { ref } from "vue";
import ColumnSettingsDrawer from "./ColumnSettingsDrawer.vue"; // 假设新增一个列设置抽屉组件

interface Props {
  columns: TableViewColumnConfig[]; // 当前列配置
  density: TableViewDensity; // 当前表格密度
  // ... 其他 prop，如视图列表、当前视图ID等
}

const props = defineProps<Props>();
const emit = defineEmits<{ 
  (e: "update:columns", columns: TableViewColumnConfig[]): void; // 列配置更新事件
  (e: "update:density", density: TableViewDensity): void; // 密度更新事件
  // ... 其他事件，如保存视图、切换视图等
}>();

const showColumnSettings = ref(false);

// 处理列设置面板中的列可见性、顺序、固定状态、宽度等变更
const handleColumnSettingsChange = (newColumns: TableViewColumnConfig[]) => {
  emit("update:columns", newColumns);
};

const handleDensityChange = (key: TableViewDensity) => {
  emit("update:density", key);
};

// ... 其他视图管理逻辑
</script>

<template>
  <div class="table-view-toolbar">
    <!-- 密度切换菜单 -->
    <a-dropdown>
      <a-button>密度 <a-icon type="down" /></a-button>
      <template #overlay>
        <a-menu @click="handleDensityChange($event.key as TableViewDensity)">
          <a-menu-item key="compact">紧凑</a-menu-item>
          <a-menu-item key="default">默认</a-menu-item>
          <a-menu-item key="comfortable">舒适</a-menu-item>
        </a-menu>
      </template>
    </a-dropdown>

    <!-- 列设置面板触发按钮 -->
    <a-button @click="showColumnSettings = true">列设置</a-button>

    <!-- 列设置面板组件 (可能是一个独立的抽屉或弹窗组件) -->
    <ColumnSettingsDrawer 
      v-model:visible="showColumnSettings"
      :columns="columns"
      @update:columns="handleColumnSettingsChange"
    />
    
    <!-- ... 其他工具栏内容 -->
  </div>
</template>
```

#### 4.3 核心实现逻辑说明

1.  **`ProTable.vue` 组件封装**：
    *   **Props 映射**：`ProTable.vue` 将接收 `TableViewConfig` 作为 `prop`，并将其中的 `scroll`, `bordered`, `density` 等属性直接或通过计算属性映射到 Ant Design Vue `a-table` 的相应 `props`。
    *   **多级表头渲染**：通过递归遍历 `TableViewConfig.columns` 数组，如果 `TableViewColumnConfig` 包含 `children` 属性，则将其渲染为 `a-table-column-group`，实现多级表头结构。这需要一个辅助函数 `mapColumnsToAntd` 来将内部 `TableViewColumnConfig` 结构转换为 `a-table` 期望的 `columns` 数组格式。
    *   **固定表头/冻结列/横纵向滚动**：`TableViewConfig.scroll` 对象将直接绑定到 `a-table` 的 `scroll` prop，实现横纵向滚动。`TableViewColumnConfig.fixed` 属性将直接绑定到 `a-table-column` 的 `fixed` prop，实现冻结列。
    *   **列宽拖拽**：
        *   利用 `a-table` 的 `customHeaderCell` slot，在表头单元格中注入一个可拖拽的 `resize handle` 元素。
        *   通过监听 `mousedown`, `mousemove`, `mouseup` 事件，计算鼠标拖拽的距离，并更新对应列的 `width`。
        *   更新后的 `width` 值将通过 `emit("update:config", newConfig)` 事件向上通知，由父组件（通常是使用 `useTableView` 的页面）接收并更新 `TableViewConfig`，进而触发 `useTableView` 的自动保存机制。
        *   需要注意拖拽时的边界限制（`minWidth`, `maxWidth`）和性能优化（节流/防抖）。
    *   **自适应列宽**：这通常是一个更复杂的特性，可能需要结合 `ResizeObserver` 监听表格容器宽度变化，并根据预设的列宽分配策略（例如，剩余空间按比例分配给未设置固定宽度的列）动态调整列宽。这可能需要一个独立的 Composable 或在 `ProTable.vue` 内部实现。
    *   **单元格省略 tooltip**：利用 `a-table` 的 `bodyCell` slot，当 `TableViewColumnConfig.ellipsis` 和 `TableViewColumnConfig.tooltip` 都为 `true` 时，在单元格内容外层包裹 `a-tooltip` 组件，并配合 CSS 实现文本省略。
    *   **合并单元格**：利用 `a-table` 的 `customCell` 或 `onCell` 属性，结合 `useTableView` 中已有的 `computeMergeSpans` 逻辑，动态计算每个单元格的 `rowSpan` 和 `colSpan`。
    *   **斑马纹/边框/紧凑模式**：`TableViewConfig.stripe` 绑定到 `a-table` 的 `row-class-name` 或直接通过 `bordered` 和 `size` prop 实现。

2.  **`useTableView.ts` Composable 改造**：
    *   **状态管理**：扩展 `TableViewState` 以包含所有新的表头与布局配置。确保 `useTableView` 能够响应式地管理这些配置。
    *   **持久化逻辑**：修改 `saveView`, `updateTableViewConfig` 等方法，使其能够正确地将包含多级表头、列宽、固定列等信息的 `TableViewConfig` 序列化并存储到后端。
    *   **列宽持久化**：当用户拖拽列宽时，`ProTable.vue` 会发出 `update:config` 事件，`useTableView` 接收到此事件后，应更新内部的 `TableViewConfig` 状态，并触发 `scheduleAutoSave` 进行防抖自动保存。

3.  **`table-view-toolbar.vue` 改造**：
    *   **列设置面板**：重构列设置面板，使其能够以树形结构展示多级表头，并允许用户对每一列（包括子列）进行可见性切换、顺序调整、固定状态设置、宽度调整等操作。这可能需要一个独立的 `ColumnSettingsDrawer` 或 `ColumnSettingsModal` 组件来承载复杂的配置 UI。
    *   **密度切换**：确保现有的密度切换功能与 `TableViewConfig.density` 双向绑定，并能正确更新 `useTableView` 中的状态。

### 五、与持久化视图的集成

#### 5.1 该维度的状态如何持久化到 TableViewConfig

“表头与布局能力”维度下的所有配置项都将统一持久化到扩展后的 `TableViewConfig` 对象中，并通过 `useTableView` Composable 进行管理和存储。具体持久化策略如下：

1.  **核心载体**：`TableViewConfig` 对象是所有表格视图配置的唯一持久化载体。它包含了表格的整体布局、列的详细配置以及其他视图相关的设置。

2.  **列配置持久化**：
    *   **`TableViewColumnConfig[]`**：`TableViewConfig` 中的 `columns` 属性是一个 `TableViewColumnConfig` 数组。每个 `TableViewColumnConfig` 对象将存储单列的所有布局相关属性，包括：
        *   `visible`：列的显示/隐藏状态。
        *   `order`：列的显示顺序。
        *   `width`：列的宽度，包括用户拖拽调整后的宽度。
        *   `minWidth`, `maxWidth`：列的最小/最大宽度限制。
        *   `fixed`：列的固定状态（左固定、右固定、不固定）。
        *   `align`：列内容的对齐方式。
        *   `ellipsis`, `tooltip`：单元格内容省略和 tooltip 提示的开关。
        *   `resizable`：列是否可拖拽调整宽度。
        *   `colSpan`, `rowSpan`：表头单元格的合并信息。
        *   `children`：用于多级表头和分组列的嵌套列配置。
    *   **JSON 序列化**：由于 `TableViewColumnConfig` 包含了嵌套的 `children` 属性，整个 `columns` 数组将以 JSON 格式序列化后存储到后端数据库中 `TableViewConfig` 对应的字段（通常是一个 JSONB 或 TEXT 字段）。

3.  **表格整体布局持久化**：
    *   **`density`**：表格的密度（紧凑、默认、舒适）将直接存储在 `TableViewConfig.density` 字段中。
    *   **`bordered`**：表格是否显示边框将存储在 `TableViewConfig.bordered` 字段中。
    *   **`stripe`**：表格是否显示斑马纹将存储在 `TableViewConfig.stripe` 字段中。
    *   **`scroll`**：表格的横纵向滚动配置（`x`, `y`）将存储在 `TableViewConfig.scroll` 对象中。

4.  **合并单元格规则持久化**：
    *   `TableViewConfig.mergeCells` 属性将继续存储现有的 `MergeCellRule[]`，用于定义数据行的单元格合并规则。

5.  **`useTableView` 的作用**：
    *   `useTableView` Composable 负责从后端 API (`services/api-system.ts`) 获取 `TableViewConfig`，并将其转换为响应式状态供 `ProTable.vue` 和 `table-view-toolbar.vue` 使用。
    *   当用户在前端界面（如列设置面板、列宽拖拽）修改了任何表头或布局相关的配置时，`useTableView` 会捕获这些变更，并更新其内部的 `TableViewConfig` 响应式对象。
    *   最终，`useTableView` 会调用后端 API (`updateTableViewConfig` 或 `updateTableView`) 将更新后的 `TableViewConfig` 持久化到数据库。

#### 5.2 自动保存 vs 手动保存策略

SecurityPlatform 项目已有的 `useTableView.ts` 中包含了 `scheduleAutoSave` (400ms 防抖) 功能，这为“表头与布局能力”的持久化提供了良好的基础。我们将采用**自动保存为主，手动保存为辅**的策略。

1.  **自动保存 (推荐)**：
    *   **触发时机**：
        *   **列宽拖拽**：当用户拖拽调整列宽并释放鼠标后，`ProTable.vue` 会发出 `update:config` 事件，`useTableView` 接收后，通过 `scheduleAutoSave` 触发自动保存。
        *   **列设置面板变更**：在 `table-view-toolbar.vue` 的列设置面板中，用户修改列的可见性、顺序、固定状态、宽度、多级表头结构等操作后，点击“确定”或关闭面板时，触发 `update:columns` 事件，进而由 `useTableView` 触发自动保存。
        *   **密度切换**：用户切换表格密度后，触发 `update:density` 事件，由 `useTableView` 触发自动保存。
    *   **用户体验**：自动保存可以提供流畅的用户体验，用户无需频繁点击保存按钮。在保存成功后，可以提供一个轻量级的提示（如右上角弹出“配置已保存”的 Toast）。
    *   **防抖机制**：`scheduleAutoSave` 的 400ms 防抖机制可以有效避免频繁的 API 请求，提高性能。

2.  **手动保存 (补充)**：
    *   **触发时机**：
        *   **视图“另存为”**：用户可以将当前配置保存为一个新的视图，这属于明确的手动保存操作。
        *   **视图“保存”**：用户可以显式点击“保存”按钮，将当前视图的配置立即持久化。这在用户进行了一系列复杂操作，希望立即确保配置生效时非常有用。
        *   **设为默认视图**：将当前视图配置设为默认视图，也是一种手动保存操作。
    *   **用户体验**：手动保存通常伴随着更明确的成功或失败反馈，并允许用户在保存前进行确认。

3.  **冲突解决**：
    *   如果用户在短时间内进行了多次操作，自动保存的防抖机制会确保只有最后一次有效的配置被发送到后端。
    *   如果同时存在自动保存和手动保存的场景（例如，用户正在拖拽列宽，同时点击了“保存”按钮），需要确保手动保存的优先级更高，或者在手动保存时取消正在进行的自动保存请求。

通过这种策略，我们可以确保用户对表格表头和布局的修改能够及时、有效地持久化，同时兼顾用户体验和系统性能。

### 六、低代码（AMIS）集成方案

#### 6.1 如何在 AMIS Schema 中支持该能力

在 AMIS 低代码平台中支持“表头与布局能力”维度的核心在于将扩展后的 `TableViewConfig` 映射到 AMIS `table` 组件的 Schema 结构。AMIS 的 `table` 组件本身提供了丰富的配置项来支持表格的布局能力，我们可以通过以下方式进行映射：

1.  **基础属性映射**：
    *   **`columns` (列配置)**：AMIS `table` 组件的 `columns` 属性支持嵌套结构，可以直接映射 `TableViewConfig.columns` 中的 `children` 属性来实现多级表头和分组列。`TableViewColumnConfig` 中的 `key`, `dataIndex`, `title`, `width`, `fixed`, `align`, `ellipsis` 等属性可以直接映射到 AMIS `column` 对象的相应属性。
        *   `fixed`: AMIS `column` 支持 `fixed: 'left'` 或 `fixed: 'right'`。
        *   `width`: AMIS `column` 支持 `width` 属性。
        *   `ellipsis`: AMIS `column` 支持 `ellipsis: true`，并通常自动带有 `tooltip`。
    *   **`scroll` (横纵向滚动)**：`TableViewConfig.scroll.x` 和 `TableViewConfig.scroll.y` 可以映射到 AMIS `table` 组件的 `scrollX` 和 `scrollY` 属性。
    *   **`bordered` (边框)**：`TableViewConfig.bordered` 可以映射到 AMIS `table` 组件的 `bordered` 属性。
    *   **`stripe` (斑马纹)**：`TableViewConfig.stripe` 可以映射到 AMIS `table` 组件的 `stripe` 属性。
    *   **`density` (紧凑模式)**：AMIS `table` 组件通常通过 `className` 或 `size` 属性来控制密度，可以将其映射到 `TableViewConfig.density`。

2.  **复杂交互映射**：
    *   **列宽拖拽**：AMIS `table` 组件原生支持 `resizable: true` 来启用列宽拖拽。当用户拖拽列宽时，AMIS 会触发相应的事件，我们可以监听这些事件，并将新的列宽同步回 `TableViewConfig`，进而触发持久化。
    *   **合并单元格**：AMIS `table` 组件的 `columns` 配置中，每个 `column` 都可以定义 `rowSpanExpr` 和 `colSpanExpr` 来实现单元格合并。这需要将 `useTableView` 中 `computeMergeSpans` 的逻辑转换为 AMIS 表达式或在数据预处理阶段计算好 `rowspan`/`colspan` 值。

**AMIS Schema 示例**：

```json
{
  "type": "table",
  "source": "${api}",
  "columns": [
    {
      "type": "text",
      "name": "id",
      "label": "ID",
      "width": 80,
      "fixed": "left",
      "resizable": true
    },
    {
      "label": "用户信息",
      "children": [
        {
          "type": "text",
          "name": "username",
          "label": "用户名",
          "width": 150,
          "ellipsis": true,
          "tooltip": true
        },
        {
          "type": "text",
          "name": "email",
          "label": "邮箱",
          "width": 200
        }
      ]
    },
    {
      "label": "操作",
      "fixed": "right",
      "width": 100,
      "buttons": [
        {
          "type": "button",
          "label": "编辑"
        }
      ]
    }
  ],
  "scrollX": true, // 对应 TableViewConfig.scroll.x
  "bordered": true, // 对应 TableViewConfig.bordered
  "stripe": true, // 对应 TableViewConfig.stripe
  "density": "compact" // 对应 TableViewConfig.density
}
```

#### 6.2 是否需要注册自定义 AMIS 插件

-   **必要性**：对于 AMIS 原生 `table` 组件无法直接支持的复杂交互或特殊渲染需求，例如：
    *   **高度定制化的列设置面板**：如果 `table-view-toolbar.vue` 中的列设置面板功能非常强大且复杂，AMIS 原生可能无法提供同等灵活度的配置界面。此时，可以考虑注册一个自定义的 AMIS 组件，将 Vue 的 `table-view-toolbar.vue` 封装进去。
    *   **高级表头合并渲染**：如果 AMIS 原生 `columns` 的 `colSpan`/`rowSpan` 无法满足表头合并的复杂逻辑，可能需要自定义 `column` 类型或 `headerCellRender`。
    *   **与 `useTableView` 的深度集成**：为了在 AMIS 运行时也能利用 `useTableView` 的持久化能力（例如用户在 AMIS 渲染的表格中调整列宽后自动保存），可能需要自定义 AMIS 组件，并在其中集成 `useTableView` 的逻辑。
-   **建议**：
    *   **初期**：优先利用 AMIS 现有 `table` 组件的能力，通过 Schema 映射实现大部分表头与布局功能。对于多级表头、固定列、列宽拖拽等，AMIS 都有较好的支持。
    *   **进阶**：如果需要将 Vue 的 `table-view-toolbar.vue` 这样的复杂交互组件无缝集成到 AMIS 编辑器和运行时中，或者需要实现 AMIS 原生不支持的布局特性，则需要注册自定义 AMIS 插件。
    *   **`business-plugins.ts`**：可以利用现有 `business-plugins.ts` 文件来注册新的自定义 AMIS 组件或插件，例如 `atlas-pro-table`，它内部封装 `ProTable.vue` 并处理与 `TableViewConfig` 的双向绑定。

### 七、优先级与实施建议

#### 7.1 基础版/进阶版/高阶版分级

-   **基础版 (P0)**：
    *   **核心能力**：固定表头/冻结列、横纵向滚动、单元格省略tooltip、行高（通过密度控制）、合并单元格（现有 `computeMergeSpans` 集成）。
    *   **实现方式**：主要通过扩展 `TableViewColumnConfig` 和 `TableViewConfig`，并改造 `useTableView.ts` 和 `ProTable.vue`，将配置映射到 Ant Design Vue `a-table` 的原生 `props`。
    *   **AMIS 集成**：通过 Schema 映射，利用 AMIS `table` 组件的现有能力支持。
-   **进阶版 (P1)**：
    *   **核心能力**：多级表头/复杂表头、分组列、列宽拖拽、斑马纹/边框/紧凑模式。
    *   **实现方式**：
        *   多级表头：扩展 `TableViewColumnConfig` 的 `children` 属性，`ProTable.vue` 递归渲染。
        *   列宽拖拽：在 `ProTable.vue` 中实现列宽拖拽的 UI 交互，并更新 `TableViewConfig`。
        *   斑马纹/边框：扩展 `TableViewConfig` 属性，并绑定到 `a-table`。
    *   **AMIS 集成**：AMIS `table` 组件原生支持多级表头和列宽拖拽。斑马纹/边框通过 Schema 映射。
-   **高阶版 (P2)**：
    *   **核心能力**：动态表头（完全动态生成）、自适应列宽、表头单元格 `colSpan`/`rowSpan` 的可视化配置和渲染。
    *   **实现方式**：
        *   动态表头：需要更复杂的 `useTableView` 逻辑来处理动态 `columns` 数组的生成和更新。
        *   自适应列宽：可能需要引入 `ResizeObserver` 和复杂的布局算法。
        *   表头合并：可能需要自定义 `a-table` 的 `customHeaderCell` 或更深层次的组件定制。
    *   **AMIS 集成**：对于高度定制化的表头合并和动态表头，可能需要注册自定义 AMIS 插件，将 `ProTable.vue` 封装为 AMIS 组件。

#### 7.2 预估工作量（人天）

-   **基础版 (P0)**：5 人天
    *   扩展类型定义：0.5 人天
    *   `useTableView.ts` 改造（集成现有合并单元格，处理 `fixed`、`ellipsis`、`density`）：1.5 人天
    *   `ProTable.vue` 封装（基础 `a-table` 映射，处理 `scroll`、`bordered`、`stripe`）：2 人天
    *   `table-view-toolbar.vue` 改造（密度切换，固定列设置）：1 人天
-   **进阶版 (P1)**：8 人天
    *   `TableViewColumnConfig` 扩展 `children`、`resizable`：0.5 人天
    *   `ProTable.vue` 实现多级表头渲染、列宽拖拽 UI 逻辑：4 人天
    *   `useTableView.ts` 处理列宽持久化：1.5 人天
    *   `table-view-toolbar.vue` 改造（支持多级表头配置界面）：2 人天
-   **高阶版 (P2)**：12 人天
    *   动态表头生成逻辑：3 人天
    *   自适应列宽算法与实现：3 人天
    *   表头单元格 `colSpan`/`rowSpan` 渲染与配置：4 人天
    *   AMIS 自定义插件开发（如果需要）：2 人天

**总计预估工作量：25 人天**

#### 7.3 关键风险与注意事项

-   **Ant Design Vue 版本兼容性**：确保所有新功能和改造都与 Ant Design Vue 4.x 版本兼容，特别是其 `a-table` 组件的 `props` 和 `slots`。
-   **性能问题**：
    *   **复杂表头渲染**：多级表头和大量列可能导致渲染性能下降，需要关注 `a-table` 的虚拟滚动能力（如果需要）和渲染优化。
    *   **列宽拖拽**：频繁的列宽调整可能触发大量重绘，需要进行节流或防抖处理。
-   **状态管理复杂性**：`TableViewConfig` 结构会变得更加复杂，`useTableView` 需要处理更深层次的响应式数据更新和持久化逻辑，容易引入 bug。
-   **低代码集成挑战**：
    *   **AMIS Schema 映射**：将复杂的 `TableViewConfig` 完整且优雅地映射到 AMIS Schema 中可能存在挑战，特别是对于一些 AMIS 原生不支持的特性。
    *   **Vue/React 混合渲染**：在 AMIS (React) 中渲染 Vue 组件 (`ProTable.vue`) 需要确保数据流和事件处理的顺畅，避免出现隔离问题。
-   **用户体验**：
    *   **列设置面板**：复杂表头的配置界面设计需要考虑用户友好性，避免过于复杂难以操作。
    *   **自动保存反馈**：确保自动保存有清晰的视觉反馈，避免用户困惑。
-   **测试覆盖**：由于表格组件的复杂性，需要编写充分的单元测试和端到端测试，确保所有功能在不同配置下都能正常工作。
-   **后端 API 配合**：确保后端 `TableViewConfig` 的存储和读取 API 能够支持扩展后的复杂结构，特别是对于 `columns` 数组中的嵌套和新增属性。

---

## 维度 2：列管理能力

- **现状评估**：部分
- **实施优先级**：P0
- **预估人天**：25 天

# 维度2：列管理能力封装方案

## 一、现状分析

### 代码库中已有哪些相关实现

根据 `codebase_summary.md` 文件，SecurityPlatform 项目在列管理方面已有一些基础实现：

1.  **`composables/useTableView.ts`**：
    -   **类型定义**：包含 `TableViewColumn` 和 `ColumnSettingItem`，用于描述列的基本属性。
    -   **列配置管理**：支持 `visible`（显示/隐藏）、`order`（顺序）、`width`（宽度）、`pinned`（固定列）和 `align`（对齐方式）等属性的配置和管理。
    -   **视图持久化**：提供了 `saveView`, `saveAs`, `setDefault`, `resetToDefault` 等功能，表明列配置作为视图的一部分是可以被持久化的。

2.  **`components/table/table-view-toolbar.vue`**：
    -   **列设置面板**：提供了用户界面，允许用户进行“显示/隐藏”、“上移/下移”（对应列顺序调整）和“固定左右”等操作。这表明前端已经有交互界面来修改部分列属性。

3.  **`types/api.ts`**：
    -   **`TableViewColumnConfig`**：定义了列配置的详细结构，包括 `key`, `visible`, `order`, `width`, `pinned`, `align`, `ellipsis`, `wrap`, `tooltip`。这与 `useTableView.ts` 中的列配置管理能力相呼应。
    -   **`TableViewConfig`**：包含 `columns` 字段，用于存储表格的所有列配置，是列管理能力持久化的核心数据结构。

4.  **`types/dynamic-tables.ts`**：
    -   **`DynamicFieldPermissionRule`**：定义了字段权限规则，包含 `fieldName`, `roleCode`, `canView`, `canEdit`。这为列权限控制提供了后端支持的类型基础。

### 存在哪些缺口和不足

尽管现有代码库已具备列管理的基础，但仍存在以下缺口和不足：

1.  **列顺序拖拽**：虽然有“上移/下移”功能，但缺乏直观的列头拖拽调整顺序能力，用户体验有待提升。
2.  **列宽记忆**：`TableViewColumnConfig` 中包含 `width` 字段，但未明确说明其是否支持用户手动调整列宽后的记忆功能，以及如何与自动布局协同。
3.  **列分组**：现有类型和功能中未提及列分组（多级表头）的能力，这对于复杂表格场景是缺失的。
4.  **列权限控制（前端渲染层）**：`codebase_summary.md` 明确指出“后端API已有，前端未集成”，这意味着虽然后端可以提供字段权限，但前端尚未将其应用于列的显示/隐藏或交互控制。
5.  **列配置持久化的完整性**：虽然有视图持久化能力，但对于“列分组”、“列权限控制”等新增能力，需要确保其状态也能被完整持久化。
6.  **恢复默认**：虽然 `useTableView.ts` 提供了 `resetToDefault`，但需要明确其恢复的是哪种“默认”状态（系统默认、用户首次配置默认等）。
7.  **列状态模型扩展**：`TableViewColumnConfig` 已经包含了 `visible`, `width`, `order`, `pinned`，但对于 `sort`（排序状态）和 `filter`（筛选状态）的列级别持久化，现有类型中未明确体现。虽然 `TableViewSort` 和 `TableViewFilter` 存在于 `types/api.ts` 中，但它们是作为 `TableViewConfig` 的顶级属性，而非单个列的属性。

## 二、封装目标

本维度的封装目标是提供一套全面、灵活且用户友好的表格列管理能力，并确保其在普通面板和低代码场景下的兼容性。

### 该维度需要封装哪些核心能力

1.  **隐藏列/显示列**：提供用户界面（如列设置面板）和编程接口，允许用户或开发者控制列的可见性。已部分实现，需完善UI和状态管理。
2.  **列顺序拖拽**：实现列头拖拽功能，允许用户通过拖拽调整列的显示顺序，并实时更新列配置。
3.  **列宽记忆**：支持用户手动调整列宽后，将新的列宽值持久化，并在下次加载时恢复。
4.  **固定列设置**：允许用户将列固定在表格的左侧或右侧，以方便查看。已部分实现，需确保与拖拽、隐藏等功能的兼容性。
5.  **列分组**：支持多级表头，允许将相关列进行逻辑分组，提升表格的可读性。
6.  **列权限控制**：根据用户角色或权限，动态控制列的可见性或可操作性（如是否可编辑）。需要集成后端提供的字段权限API。
7.  **列配置持久化**：确保所有列管理相关的用户配置（可见性、顺序、宽度、固定状态、分组、权限等）都能被保存到 `TableViewConfig` 中，并在下次加载时恢复。
8.  **恢复默认**：提供“恢复默认”功能，允许用户将列配置重置为系统预设或管理员配置的默认状态。
9.  **列状态模型 `visible+fixed+width+order+sort+filter`**：构建一个包含这些属性的完整列状态模型，并确保其可被持久化。

### 普通面板场景 vs 低代码场景的差异处理

-   **普通面板场景**：主要通过 Vue 组件和 Composable 提供开箱即用的功能。用户通过 Ant Design Vue 的表格组件配置和 `table-view-toolbar.vue` 提供的UI进行交互。封装方案将直接增强现有 `useTableView.ts` 和相关组件。
-   **低代码场景（AMIS）**：AMIS 自身提供了强大的表格能力。封装的重点在于如何将 Vue 侧的列管理能力映射到 AMIS Schema 中，或者通过注册自定义 AMIS 插件来扩展 AMIS 表格的功能。对于 AMIS，可能需要提供一个自定义的 `ColumnSetting` 控件或扩展其 `Table` 组件的 `columns` 配置，以支持更丰富的列管理属性。列权限控制可能需要通过 AMIS 的 `visibleOn` 或 `hiddenOn` 表达式结合后端权限数据实现。

## 三、核心数据模型 / 类型定义

为了支持完整的列管理能力，我们需要扩展现有的 `TableViewColumnConfig` 和 `TableViewConfig` 类型。

### TypeScript 接口/类型定义

```typescript
// types/api.ts (扩展或新增)

/**
 * 列权限配置
 */
export interface ColumnPermissionConfig {
  canView?: boolean; // 是否可见
  canEdit?: boolean; // 是否可编辑
  // 更多权限控制，如 canSort, canFilter 等
}

/**
 * 列分组配置
 */
export interface ColumnGroupConfig {
  title: string; // 分组标题
  children: string[]; // 包含的列的 key 数组
  // 更多分组属性，如可折叠等
}

/**
 * 扩展 TableViewColumnConfig，增加更多列管理属性
 */
export interface TableViewColumnConfig {
  key: string; // 列的唯一标识
  title?: string; // 列头显示名称
  visible?: boolean; // 是否可见
  order?: number; // 列的显示顺序
  width?: number | string; // 列宽，支持数字或百分比
  pinned?: 'left' | 'right' | false; // 固定列方向
  align?: 'left' | 'center' | 'right'; // 对齐方式
  ellipsis?: boolean; // 是否省略
  wrap?: boolean; // 是否自动换行
  tooltip?: boolean; // 是否显示 tooltip
  sortable?: boolean; // 是否可排序
  sortOrder?: 'ascend' | 'descend' | false; // 排序状态
  filterable?: boolean; // 是否可筛选
  filterValue?: any[]; // 筛选值
  groupKey?: string; // 所属分组的 key
  permission?: ColumnPermissionConfig; // 列权限配置
  // ... 其他 Ant Design Vue Table Column 支持的属性
}

/**
 * 扩展 TableViewConfig，增加列分组配置
 */
export interface TableViewConfig {
  columns: TableViewColumnConfig[]; // 列配置数组
  density?: TableViewDensity; // 密度
  pagination?: TableViewPagination; // 分页配置
  sort?: TableViewSort; // 全局排序配置 (如果列级别排序不满足，可保留全局)
  filters?: TableViewFilter[]; // 全局筛选配置 (如果列级别筛选不满足，可保留全局)
  groupBy?: TableViewGroupBy[]; // 全局分组配置
  aggregations?: TableViewAggregation[]; // 聚合配置
  queryPanel?: TableViewQueryPanel; // 查询面板配置
  queryModel?: TableViewQueryModel; // 查询模型配置
  mergeCells?: MergeCellRule[]; // 合并单元格规则
  columnGroups?: ColumnGroupConfig[]; // 新增：列分组配置
  // ... 其他现有属性
}

// 现有类型保持不变，或根据需要进行调整
// export type TableViewDensity = 'compact' | 'default' | 'comfortable';
// export interface TableViewPagination { /* ... */ }
// export interface TableViewSort { /* ... */ }
// export interface TableViewFilter { /* ... */ }
// export interface TableViewGroupBy { /* ... */ }
// export interface TableViewAggregation { /* ... */ }
// export interface TableViewQueryPanel { /* ... */ }
// export interface TableViewQueryModel { /* ... */ }
// export interface MergeCellRule { /* ... */ }
```

### 说明与现有 TableViewConfig 的集成方式

-   **`TableViewColumnConfig` 扩展**：在 `types/api.ts` 中，直接在现有 `TableViewColumnConfig` 接口上增加 `sortable`, `sortOrder`, `filterable`, `filterValue`, `groupKey`, `permission` 等属性。这些属性将直接存储在每个列的配置中，方便管理和持久化。
-   **`TableViewConfig` 扩展**：在 `types/api.ts` 中，为 `TableViewConfig` 接口新增 `columnGroups?: ColumnGroupConfig[]` 属性，用于存储列分组的定义。这样，多级表头的结构也能随视图一起持久化。
-   **兼容性**：由于是扩展现有接口，对于旧的 `TableViewConfig` 数据，新增的字段将是 `undefined`，不会导致运行时错误。在读取旧数据时，需要提供合理的默认值或兼容逻辑。

## 四、组件/Composable 封装方案

### 新增或改造哪些文件

1.  **改造 `composables/useTableView.ts`**：
    -   **核心逻辑增强**：增加处理列顺序拖拽、列宽调整、列分组、列权限控制的逻辑。
    -   **状态管理**：扩展 `TableViewState` 以包含更丰富的列状态，如 `columnGroups`。
    -   **API集成**：集成后端字段权限API，根据权限数据动态调整列的 `visible` 属性。

2.  **改造 `components/table/table-view-toolbar.vue`**：
    -   **列设置面板增强**：扩展现有列设置面板，增加列拖拽排序的UI（例如，使用 `vue-draggable-next` 或 Ant Design Vue 的 `Transfer` 组件实现），以及列分组的配置入口。
    -   **列宽调整**：确保列宽调整的事件能够被捕获并更新到 `useTableView` 的状态中。

3.  **新增 `components/table/ColumnGroupSetting.vue` (可选)**：
    -   如果列分组的配置逻辑较为复杂，可以考虑单独封装一个组件，用于在列设置面板中配置列分组。

4.  **新增 `composables/useColumnManager.ts` (可选)**：
    -   如果 `useTableView.ts` 变得过于庞大，可以将列管理相关的核心逻辑（如列的可见性、顺序、宽度、固定状态的计算和更新）抽离成一个新的 Composable，供 `useTableView.ts` 使用。

### 关键 Props/Emits/Expose 设计

以 `useTableView.ts` 为例，其 `expose` 的核心属性和方法可能包括：

```typescript
// composables/useTableView.ts (部分 expose)

interface UseTableViewExpose {
  // ... 现有 expose
  columns: Ref<TableViewColumnConfig[]>; // 经过处理后的最终列配置，包含可见性、顺序、宽度、固定状态、权限等
  columnGroups: Ref<ColumnGroupConfig[]>; // 列分组配置
  updateColumnConfig: (key: string, config: Partial<TableViewColumnConfig>) => void; // 更新单个列配置
  updateColumnOrder: (newOrder: string[]) => void; // 更新列顺序（通过 key 数组）
  updateColumnWidth: (key: string, width: number | string) => void; // 更新列宽
  updateColumnPinned: (key: string, pinned: 'left' | 'right' | false) => void; // 更新固定列状态
  updateColumnVisibility: (key: string, visible: boolean) => void; // 更新列可见性
  resetColumnsToDefault: () => void; // 恢复列配置到默认状态
  // ... 其他与视图持久化相关的 expose
}

export function useTableView(tableKey: string, initialConfig?: TableViewConfig): UseTableViewExpose {
  // ... 实现逻辑
}
```

### 核心实现逻辑说明

1.  **列状态管理**：
    -   `useTableView.ts` 内部维护一个响应式的 `columns` 数组（类型为 `TableViewColumnConfig[]`），作为表格最终渲染的列配置。
    -   所有对列的修改（可见性、顺序、宽度、固定状态、排序、筛选、权限）都应通过 `useTableView` 提供的统一接口进行，确保状态的一致性。

2.  **列顺序拖拽**：
    -   在 `table-view-toolbar.vue` 的列设置面板中，使用一个可拖拽的列表组件（如 `vue-draggable-next`）来展示列的顺序。
    -   当用户拖拽调整顺序后，触发 `updateColumnOrder` 事件，`useTableView` 接收新的列 `key` 数组，并更新内部 `columns` 数组的 `order` 属性。
    -   Ant Design Vue 的 `a-table` 组件可以通过 `columns` 属性的顺序来控制渲染顺序，因此只需更新 `columns` 数组即可。

3.  **列宽记忆**：
    -   在 `a-table` 的 `resizable` 属性启用后，监听 `resize` 事件，获取调整后的列宽。
    -   将新的列宽通过 `updateColumnWidth` 方法更新到 `useTableView` 的状态中，并触发持久化。

4.  **固定列设置**：
    -   在列设置面板中提供切换固定左右的选项。
    -   调用 `updateColumnPinned` 方法更新列的 `pinned` 属性。

5.  **列分组**：
    -   在 `useTableView.ts` 中，根据 `columnGroups` 和 `columns` 生成 Ant Design Vue `a-table` 所需的多级表头结构。
    -   列设置面板中提供列分组的配置界面，允许用户创建、编辑和删除分组，并将配置更新到 `columnGroups` 状态。

6.  **列权限控制**：
    -   在 `useTableView.ts` 初始化时，通过调用后端 API 获取当前用户的字段权限数据。
    -   将权限数据与 `TableViewColumnConfig` 中的 `permission` 属性进行合并或计算，最终决定列的 `visible` 状态。
    -   对于 `canEdit` 权限，可以在 `editable-cell.vue` 或其他编辑组件中进行判断，控制单元格是否可编辑。

7.  **恢复默认**：
    -   `resetColumnsToDefault` 方法应从后端获取系统默认的 `TableViewConfig` 或项目预设的默认配置，然后更新 `useTableView` 的内部状态。

## 五、与持久化视图的集成

### 该维度的状态如何持久化到 TableViewConfig

所有列管理相关的状态都将统一存储在 `TableViewConfig` 对象中，并通过 `services/api-system.ts` 提供的 API 进行持久化。

-   **`TableViewColumnConfig` 属性**：`visible`, `order`, `width`, `pinned`, `sortOrder`, `filterValue`, `groupKey`, `permission` 等属性将直接作为 `TableViewConfig.columns` 数组中每个 `TableViewColumnConfig` 对象的字段进行存储。
-   **`ColumnGroupConfig` 属性**：新增的 `columnGroups` 数组将作为 `TableViewConfig` 的顶级属性进行存储，用于描述列的分组结构。

当用户进行任何列管理操作（如调整顺序、改变可见性、修改列宽、设置固定列、配置分组等）时，`useTableView.ts` 会更新其内部的响应式状态，并通过防抖机制（`scheduleAutoSave`）触发 `updateTableViewConfig` API 调用，将最新的 `TableViewConfig` 对象发送到后端进行保存。

### 自动保存 vs 手动保存策略

-   **自动保存**：对于用户频繁操作的列管理功能（如列顺序拖拽、列宽调整、显示/隐藏列），应采用自动保存策略。`useTableView.ts` 中已有的 `scheduleAutoSave`（400ms 防抖）机制非常适合此场景，可以确保用户操作的实时性，同时避免频繁的网络请求。
-   **手动保存**：对于一些更复杂的配置，例如列分组的创建和编辑，或者涉及权限等敏感信息的修改，可以考虑提供明确的“保存”按钮，让用户手动触发保存操作。这可以避免误操作导致的不期望的自动保存，并给用户一个确认的机会。然而，考虑到用户体验，尽可能地实现自动保存是更好的选择，除非有明确的业务或安全要求。

## 六、低代码（AMIS）集成方案

### 如何在 AMIS Schema 中支持该能力

AMIS 提供了强大的 `Table` 组件，其 `columns` 属性支持丰富的配置。我们可以通过以下方式在 AMIS Schema 中支持列管理能力：

1.  **扩展 `columns` 属性**：
    -   AMIS 的 `columns` 数组中的每个对象都可以包含自定义属性。我们可以将 `TableViewColumnConfig` 中的 `visible`, `order`, `width`, `fixed`（对应 AMIS 的 `fixed`）, `sortable`, `filterable` 等属性直接映射到 AMIS 的列配置中。
    -   对于 `permission` 属性，可以利用 AMIS 的 `visibleOn` 或 `hiddenOn` 表达式，结合上下文数据（如用户权限信息）来动态控制列的可见性。
    -   对于 `groupKey` 属性，AMIS 支持 `groupName` 或通过嵌套 `columns` 实现多级表头，需要进行适当的转换。

2.  **自定义 `ColumnSetting` 控件**：
    -   AMIS 允许注册自定义组件。我们可以开发一个自定义的 `ColumnSetting` 控件，它接收 `TableViewConfig` 作为 `data` 属性，并在内部渲染一个类似 `table-view-toolbar.vue` 中的列设置面板。
    -   这个自定义控件可以提供列的显示/隐藏、顺序拖拽、固定列等功能，并将修改后的 `TableViewConfig` 通过 `onChange` 事件或 `action` 机制反馈给 AMIS Schema。

3.  **通过 `source` 动态生成 Schema**：
    -   AMIS 的 `Table` 组件的 `columns` 属性可以是一个 API 接口，动态获取列配置。我们可以让 AMIS 调用后端 API（如 `GET /table-views/default-config?tableKey={key}`），获取包含完整列管理信息的 `TableViewConfig`，然后由后端或一个中间层将其转换为 AMIS 兼容的 `columns` 数组。

### 是否需要注册自定义 AMIS 插件

-   **是，可能需要**：
    -   如果需要实现列头拖拽调整顺序、列宽拖拽记忆等高级交互功能，并且 AMIS 自身提供的 `Table` 组件无法直接满足，那么注册一个自定义 AMIS 插件是必要的。这个插件可以封装一个 Vue 组件，该组件内部使用 Ant Design Vue 的 `a-table` 或其他支持这些交互的组件，并将其渲染到 AMIS 页面中。
    -   对于列分组的复杂配置界面，如果 AMIS 现有组件难以构建，也可以考虑通过自定义插件提供。
    -   对于列权限控制，如果 `visibleOn` 表达式不足以表达复杂的权限逻辑，自定义插件可以提供更灵活的权限判断和渲染逻辑。

-   **否，如果仅是数据映射**：
    -   如果仅仅是将 `TableViewColumnConfig` 中的属性映射到 AMIS `columns` 的现有属性，并且不涉及复杂的交互或自定义UI，那么可能不需要注册自定义插件，只需在生成 AMIS Schema 时进行数据转换即可。

考虑到 SecurityPlatform 项目已经有 `AmisRenderer.vue` 和 `business-plugins.ts`，注册自定义 AMIS 插件是可行的方案，可以更好地复用 Vue 侧的列管理逻辑和UI。

## 七、优先级与实施建议

### 基础版/进阶版/高阶版分级

| 版本    | 功能点                                                              | 描述                                                                                                                               | 优先级 |
| :------ | :------------------------------------------------------------------ | :--------------------------------------------------------------------------------------------------------------------------------- | :----- |
| **基础版** | 隐藏列/显示列、列顺序调整（上移/下移）、固定列设置、列宽记忆、列配置持久化、恢复默认 | 完善现有功能，确保列的可见性、顺序、宽度、固定状态可配置、可记忆、可持久化，并提供恢复默认功能。                               | P0     |
| **进阶版** | 列顺序拖拽、列权限控制（前端渲染层）                                | 提升用户体验，允许通过拖拽调整列顺序；集成后端字段权限API，实现前端列的动态显示/隐藏。                                           | P1     |
| **高阶版** | 列分组、列状态模型（`sort`+`filter`）、低代码（AMIS）集成           | 支持多级表头，满足复杂表格展示需求；将排序和筛选状态纳入列配置持久化；探索并实现 AMIS 下的列管理能力。                         | P2     |

### 预估工作量（人天）

-   **基础版**：5 人天
    -   完善 `useTableView.ts` 的列宽记忆和状态管理。
    -   增强 `table-view-toolbar.vue` 的列设置面板UI，确保所有基础功能可用。
    -   确保 `TableViewConfig` 能够完整持久化这些基础属性。
-   **进阶版**：8 人天
    -   实现列头拖拽排序功能（可能需要引入第三方库）。
    -   集成后端字段权限 API，并在 `useTableView.ts` 中实现权限控制逻辑。
    -   修改 `a-table` 的 `columns` 属性，根据权限动态调整。
-   **高阶版**：12 人天
    -   设计并实现列分组的数据结构和渲染逻辑。
    -   扩展 `TableViewColumnConfig` 和 `TableViewConfig` 以支持列级别的排序和筛选状态持久化。
    -   研究 AMIS 的扩展机制，设计并实现 AMIS 下的列管理方案（可能包括自定义插件）。

**总预估人天数：25 人天**

### 关键风险与注意事项

1.  **Ant Design Vue Table 的限制**：`a-table` 在列拖拽、列宽调整、多级表头等方面的原生支持程度需要详细调研。如果原生支持不足，可能需要引入第三方库或进行大量自定义开发，增加工作量和复杂性。
2.  **性能问题**：频繁的列状态更新和持久化可能会带来性能开销，尤其是在列数量较多时。需要注意防抖、节流以及状态更新的优化。
3.  **低代码 AMIS 集成复杂度**：AMIS 的集成可能面临挑战，需要深入理解 AMIS 的渲染机制和扩展点。自定义插件的开发和维护成本较高。
4.  **状态同步**：确保 Vue 侧的列管理状态与 AMIS 侧的列管理状态能够有效同步，避免数据不一致。
5.  **用户体验**：列管理功能涉及复杂的交互，需要精心设计UI和交互流程，确保用户易于理解和操作。
6.  **国际化**：所有新增的UI文本和提示信息都需要支持国际化。
7.  **测试**：列管理功能涉及多种状态和交互，需要编写全面的单元测试和端到端测试，确保功能的稳定性和正确性。

## References

[1] Ant Design Vue Table Component: [https://next.antdv.com/components/table-cn](https://next.antdv.com/components/table-cn)
[2] AMIS Documentation: [https://baidu.github.io/amis/zh-CN/docs/index](https://baidu.github.io/amis/zh-CN/docs/index)
[3] vue-draggable-next GitHub Repository: [https://github.com/SortableJS/vue.draggable.next](https://github.com/SortableJS/vue.draggable.next)


---

## 维度 3：数据查看能力

- **现状评估**：部分
- **实施优先级**：P0
- **预估人天**：27 天

## 当前任务：分析维度 维度3：数据查看能力

### 一、现状分析

#### 代码库中已有哪些相关实现

- **`composables/useTableView.ts`**: 该组合式函数定义了 `TableViewConfig` 类型，其中包含了 `sort` 和 `filters` 字段，表明系统已考虑对表格数据进行排序和过滤的配置化管理。同时，`pagination` 字段的存在也说明了分页能力是视图配置的一部分。该文件还负责视图的持久化管理，这意味着用户对排序、筛选、分页等数据查看能力的配置可以被保存和加载。
- **`composables/useCrudPage.ts`**: 该通用 CRUD 组合式函数集成了 `useTableView`，并支持分页和搜索功能。通过 `buildListParams` 方法，可以自定义构建列表查询参数，这为服务端排序和筛选提供了扩展点。
- **`types/api.ts`**: 该文件定义了核心的表格视图相关类型：
  - `TableViewSort`: 用于定义排序规则。
  - `TableViewFilter`: 用于定义过滤规则。
  - `TableViewQueryGroup`: 支持嵌套查询逻辑（AND/OR），为高级筛选面板提供了类型基础。
  - `TableViewConfig`: 包含了 `pagination`、`sort`、`filters`、`groupBy`、`aggregations` 等字段，这些字段直接对应了数据查看能力中的分页、排序、筛选、分组展示和汇总行合计等需求，说明后端和类型层面已对这些能力进行了抽象和支持。
- **`types/dynamic-tables.ts`**: `DynamicRecordQueryRequest` 类型包含了 `pageIndex`、`pageSize`、`keyword`、`sortBy`、`sortDesc`、`filters` 等字段，直接对应了分页、全局搜索、服务端排序和筛选的请求参数。`DynamicColumnDef` 中的 `sortable` 和 `searchable` 字段则表明了列级别的排序和搜索能力。
- **`services/api-system.ts`**: 提供了完整的 `TableView` CRUD API，包括更新 `TableViewConfig` 的接口，这意味着前端对数据查看能力的配置修改可以持久化到后端。
- **`components/crud/CrudPageLayout.vue`**: 提供了 `search-filters` 插槽，用于放置搜索栏和筛选条件，为全局搜索和高级筛选面板提供了 UI 布局上的支持。

#### 存在哪些缺口和不足

- **高级筛选面板（条件构建器）**: `types/api.ts` 中虽然定义了 `TableViewQueryGroup` 等类型，但 `codebase_summary.md` 明确指出“UI未实现”，这意味着用户无法通过图形界面配置复杂的筛选条件。
- **树形表格**: `codebase_summary.md` 明确指出“未封装”。
- **展开行**: `codebase_summary.md` 明确指出“未封装”。
- **汇总行/合计**: `TableViewConfig` 中虽然有 `aggregations` 字段，但 `codebase_summary.md` 明确指出“未封装”，UI 层面的展示和计算逻辑缺失。
- **数据高亮/条件格式化**: `codebase_summary.md` 明确指出“未实现”，目前没有机制支持根据数据内容对单元格或行进行样式调整。
- **分组展示**: `TableViewConfig` 中有 `groupBy` 字段，但 `codebase_summary.md` 明确指出“未封装”，UI 层面的分组逻辑和展示方式缺失。
- **空态/加载态/异常态**: `codebase_summary.md` 中未提及针对表格的统一空态、加载态和异常态处理方案。虽然 Ant Design Vue 的 `a-table` 组件本身支持这些状态，但缺乏统一的封装和管理，可能导致不同页面实现不一致。
- **前端分页/排序/筛选**: 现有实现主要倾向于服务端分页、排序和筛选。对于小数据量场景，前端进行这些操作可以提升用户体验，但目前缺乏统一的封装支持。
- **多列排序**: `TableViewSort` 类型可能支持多列排序，但 `DynamicRecordQueryRequest` 中的 `sortBy/sortDesc` 字段是单数的，需要确认当前实现是否支持多列排序，以及 UI 如何呈现和后端进行交互。
- **全局搜索**: `useCrudPage.ts` 和 `DynamicRecordQueryRequest` 中有 `keyword` 字段，但全局搜索的范围和具体实现细节（例如是搜索所有列还是特定列）未明确说明，且缺乏统一的 UI 组件封装。

### 二、封装目标

该维度的封装目标是提供一套全面、灵活且易于扩展的表格数据查看能力，以满足不同业务场景的需求，并与现有系统架构无缝集成。具体目标如下：

- **排序（单列/多列）**: 支持用户通过点击表头对单列或多列数据进行升序或降序排列。封装应能处理前端排序（针对小数据量）和服务端排序（针对大数据量），并能将排序状态持久化。
- **筛选（文本/枚举/时间/数字）**: 提供多种数据类型的筛选能力，包括文本模糊匹配、枚举多选、时间范围选择和数字范围过滤。封装应支持单个列的筛选以及跨列的组合筛选，并将筛选条件持久化。
- **全局搜索**: 提供一个统一的搜索框，允许用户通过关键词对表格所有可搜索列进行快速查找。封装应能处理前端搜索和服务端搜索，并能将搜索关键词持久化。
- **高级筛选面板**: 提供一个可视化界面，允许用户通过条件构建器（如“字段 + 操作符 + 值”的形式）构建复杂的组合筛选条件（支持 AND/OR 逻辑和条件组嵌套）。封装应能将高级筛选条件转化为后端可识别的查询结构，并持久化。
- **分页（服务端/前端）**: 支持服务端分页（默认，通过 API 请求获取指定页数据）和前端分页（针对小数据量，一次性获取所有数据后在前端进行分页）。封装应提供统一的分页控制组件，并持久化分页状态。
- **服务端排序/筛选**: 确保所有排序和筛选操作都能无缝对接后端 API，将前端配置转化为后端请求参数。
- **展开行**: 提供表格行的展开功能，用于展示额外详细信息或嵌套数据。封装应提供统一的 API 和插槽机制，方便业务方自定义展开行的内容。
- **树形表格**: 支持具有层级关系的数据展示，允许用户展开和折叠父子节点。封装应能处理树形数据的加载、渲染和操作。
- **分组展示**: 允许用户根据一个或多个列对数据进行分组，并以可视化的方式展示分组结果。封装应支持分组的配置、渲染和状态持久化。
- **汇总行/合计**: 在表格底部或分组底部展示数据的汇总信息（如总和、平均值、计数等）。封装应提供灵活的配置方式，支持多种聚合函数。
- **数据高亮/条件格式化**: 提供机制，允许用户根据预设条件（如值范围、特定文本等）对表格单元格或行应用自定义样式（如背景色、字体颜色、加粗等）。封装应支持条件的配置和动态渲染。
- **空态/加载态/异常态**: 为表格提供统一的空数据、数据加载中和数据加载失败的视觉反馈。封装应提供默认的占位符和可定制的插槽。

#### 普通面板场景 vs 低代码场景的差异处理

- **普通面板场景**: 主要通过 Vue 组件的 Props 和 Emits 进行配置和交互。封装的组件应提供丰富的 Props 来控制上述各项数据查看能力，并通过 Emits 向上层组件暴露事件，以便进行数据请求或状态同步。核心数据模型直接映射到 Vue 组件的响应式状态。
- **低代码场景（AMIS）**: 需要将封装的 Vue 组件或其能力映射到 AMIS Schema。这意味着需要为 AMIS 定义自定义组件或扩展现有组件的属性，以便在 AMIS 编辑器中配置这些数据查看能力。对于复杂功能（如高级筛选面板），可能需要注册自定义 AMIS 插件或自定义渲染器来提供更丰富的交互。核心数据模型需要与 AMIS 的数据结构进行转换和适配。

### 三、核心数据模型 / 类型定义

在现有 `types/api.ts` 和 `composables/useTableView.ts` 的基础上，我们将进一步完善和扩展数据模型，以支持维度3的各项能力。核心数据模型将围绕 `TableViewConfig` 进行扩展。

```typescript
// types/api.ts 或 types/table-view.d.ts (新增)

/**
 * @description 表格视图配置
 */
export interface TableViewConfig {
  columns: TableViewColumnConfig[];
  density: TableViewDensity;
  pagination: TableViewPaginationConfig; // 扩展分页配置
  sort: TableViewSortConfig[]; // 扩展为数组，支持多列排序
  filters: TableViewFilterGroup; // 统一筛选配置，支持高级筛选面板
  keyword: string; // 全局搜索关键词
  groupBy?: TableViewGroupByConfig[]; // 分组配置
  aggregations?: TableViewAggregationConfig[]; // 汇总行配置
  expandedRowKeys?: string[]; // 展开行 keys
  treeConfig?: TableViewTreeConfig; // 树形表格配置
  conditionalFormatting?: TableViewConditionalFormattingRule[]; // 条件格式化规则
  // ... 其他现有配置
}

/**
 * @description 分页配置
 */
export interface TableViewPaginationConfig {
  current: number;
  pageSize: number;
  total?: number; // 服务端分页时需要总数
  frontend?: boolean; // 是否前端分页，默认为 false (服务端分页)
}

/**
 * @description 排序配置
 */
export interface TableViewSortConfig {
  field: string;
  order: 'ascend' | 'descend';
}

/**
 * @description 筛选条件操作符
 */
export type FilterOperator =
  | 'eq' // 等于
  | 'ne' // 不等于
  | 'gt' // 大于
  | 'ge' // 大于等于
  | 'lt' // 小于
  | 'le' // 小于等于
  | 'like' // 模糊匹配
  | 'notLike' // 不模糊匹配
  | 'in' // 包含（枚举多选）
  | 'notIn' // 不包含
  | 'between' // 范围（数字、时间）
  | 'isNull' // 为空
  | 'isNotNull'; // 不为空

/**
 * @description 单个筛选条件
 */
export interface TableViewFilterCondition {
  field: string;
  operator: FilterOperator;
  value: any; // 筛选值，根据 operator 和字段类型不同
  valueTo?: any; // 范围筛选的结束值
}

/**
 * @description 筛选条件组
 */
export interface TableViewFilterGroup {
  logic: 'AND' | 'OR';
  conditions?: TableViewFilterCondition[];
  groups?: TableViewFilterGroup[]; // 嵌套条件组
}

/**
 * @description 分组配置
 */
export interface TableViewGroupByConfig {
  field: string;
  order?: 'ascend' | 'descend';
  showGroupSummary?: boolean; // 是否显示分组汇总
}

/**
 * @description 聚合函数类型
 */
export type AggregationFunction =
  | 'sum'
  | 'avg'
  | 'count'
  | 'max'
  | 'min';

/**
 * @description 汇总行配置
 */
export interface TableViewAggregationConfig {
  field: string; // 聚合的字段
  function: AggregationFunction; // 聚合函数
  label?: string; // 汇总行的显示标签，如 '总计'
  formatter?: string; // 格式化字符串或函数名
}

/**
 * @description 树形表格配置
 */
export interface TableViewTreeConfig {
  childrenKey?: string; // 子节点字段名，默认为 'children'
  indentSize?: number; // 缩进距离
  defaultExpandAll?: boolean; // 默认展开所有行
}

/**
 * @description 条件格式化规则
 */
export interface TableViewConditionalFormattingRule {
  id: string; // 规则唯一标识
  target: 'row' | 'cell'; // 作用目标：行或单元格
  field?: string; // 如果 target 是 'cell'，指定作用字段
  condition: TableViewFilterCondition; // 格式化触发条件
  style: {
    backgroundColor?: string;
    color?: string;
    fontWeight?: 'normal' | 'bold';
    // ... 更多样式属性
  };
}

// 扩展 TableViewColumnConfig 以支持列的搜索和筛选配置
declare module './types/api' {
  export interface TableViewColumnConfig {
    searchable?: boolean; // 是否可搜索（全局搜索）
    filterable?: boolean; // 是否可筛选
    filterType?: 'text' | 'number' | 'date' | 'enum'; // 筛选类型
    filterOptions?: { label: string; value: any }[]; // 枚举筛选选项
    defaultFilterValue?: any; // 默认筛选值
    sortable?: boolean; // 是否可排序
    defaultSortOrder?: 'ascend' | 'descend'; // 默认排序顺序
    // ... 其他现有属性
  }
}
```

#### 说明与现有 `TableViewConfig` 的集成方式

上述类型定义是对现有 `types/api.ts` 中 `TableViewConfig` 的扩展和细化。通过在 `TableViewConfig` 接口中新增 `keyword`、`groupBy`、`aggregations`、`expandedRowKeys`、`treeConfig`、`conditionalFormatting` 等字段，以及将 `sort` 字段从单个对象扩展为 `TableViewSortConfig[]` 数组，并统一 `filters` 为 `TableViewFilterGroup`，实现了对数据查看能力的全面覆盖。同时，通过 `declare module` 语法扩展 `TableViewColumnConfig`，为每个列增加了 `searchable`、`filterable`、`filterType`、`sortable` 等属性，使得列级别的能力配置更加灵活。

这些扩展字段将直接存储在 `TableViewConfig` 对象中，并通过 `useTableView` 组合式函数进行管理和持久化。当用户修改表格的排序、筛选、分页等状态时，`useTableView` 会负责更新 `TableViewConfig` 对象，并通过 `api-system.ts` 提供的接口将其保存到后端。

### 四、组件/Composable 封装方案

为了实现维度3的各项数据查看能力，我们将采取以下组件/Composable 封装方案：

#### 新增或改造哪些文件

- **`composables/useTableView.ts` (改造)**: 扩展其内部状态管理，使其能够响应和存储 `TableViewConfig` 中新增的 `keyword`、`groupBy`、`aggregations`、`expandedRowKeys`、`treeConfig`、`conditionalFormatting` 等字段。同时，需要增加处理多列排序、高级筛选逻辑的方法。
- **`composables/useTableData.ts` (新增)**: 负责表格数据的加载、处理和状态管理。它将接收 `TableViewConfig` 作为输入，根据其中的分页、排序、筛选、搜索、分组等配置，向后端请求数据或在前端处理数据。该 Composable 将封装数据请求逻辑、加载状态、错误处理等，并暴露 `dataSource`、`loading`、`error` 等响应式数据。
- **`components/table/ProTable.vue` (新增)**: 作为一个高级表格组件，它将封装 Ant Design Vue 的 `a-table`，并集成 `useTableView` 和 `useTableData`。`ProTable` 将负责渲染表格、处理用户交互（如点击排序、筛选、分页），并根据 `TableViewConfig` 动态渲染各种数据查看功能的 UI。
- **`components/table/filters/AdvancedFilterPanel.vue` (新增)**: 高级筛选面板组件，提供条件构建器 UI，允许用户构建复杂的筛选条件，并输出 `TableViewFilterGroup` 结构。
- **`components/table/toolbar/TableViewToolbar.vue` (改造)**: 在现有工具栏基础上，增加全局搜索框、高级筛选入口、密度切换、列设置等功能的 UI 控件。
- **`components/table/renderers/ConditionalFormattingRenderer.vue` (新增)**: 用于根据条件格式化规则动态渲染单元格或行的样式。
- **`components/table/renderers/SummaryRow.vue` (新增)**: 用于渲染汇总行或分组汇总行。

#### 关键 Props/Emits/Expose 设计

**`components/table/ProTable.vue`**

```typescript
// Props
interface ProTableProps {
  tableKey: string; // 唯一标识，用于视图持久化
  columns: TableViewColumnConfig[]; // 列配置
  requestApi: (params: TableViewQueryParams) => Promise<TableDataResponse>; // 数据请求API
  initialConfig?: Partial<TableViewConfig>; // 初始表格配置
  enableAdvancedFilter?: boolean; // 是否启用高级筛选
  enableTreeTable?: boolean; // 是否启用树形表格
  enableGrouping?: boolean; // 是否启用分组
  enableSummaryRow?: boolean; // 是否启用汇总行
  enableConditionalFormatting?: boolean; // 是否启用条件格式化
  // ... 其他 Ant Design Vue a-table 的原生 Props
}

// Emits
interface ProTableEmits {
  (event: 'configChange', config: TableViewConfig): void; // 配置变更事件
  (event: 'dataLoad', data: any[]): void; // 数据加载完成事件
  (event: 'error', error: any): void; // 错误事件
}

// Expose
interface ProTableExpose {
  refresh: () => void; // 刷新表格数据
  getCurrentConfig: () => TableViewConfig; // 获取当前表格配置
}
```

**`composables/useTableData.ts`**

```typescript
interface UseTableDataOptions {
  tableKey: string;
  requestApi: (params: TableViewQueryParams) => Promise<TableDataResponse>;
  config: Ref<TableViewConfig>; // 响应式的表格配置
}

interface UseTableDataResult {
  dataSource: Ref<any[]>;
  loading: Ref<boolean>;
  error: Ref<any>;
  pagination: Ref<TableViewPaginationConfig>;
  refresh: () => void;
}

function useTableData(options: UseTableDataOptions): UseTableDataResult;
```

**`components/table/filters/AdvancedFilterPanel.vue`**

```typescript
// Props
interface AdvancedFilterPanelProps {
  fields: TableViewColumnConfig[]; // 可筛选的字段列表
  initialFilterGroup?: TableViewFilterGroup; // 初始筛选条件组
}

// Emits
interface AdvancedFilterPanelEmits {
  (event: 'change', filterGroup: TableViewFilterGroup): void; // 筛选条件组变更事件
  (event: 'apply', filterGroup: TableViewFilterGroup): void; // 应用筛选事件
}
```

#### 核心实现逻辑说明

1.  **`ProTable.vue`**: 作为入口组件，它将接收 `tableKey` 和 `columns` 等基本配置。内部会使用 `useTableView` 来管理和持久化 `TableViewConfig`，并使用 `useTableData` 来处理数据加载。它会监听 `TableViewConfig` 的变化，并将其传递给 `useTableData` 进行数据请求。同时，它会根据 `TableViewConfig` 中的各项配置，动态渲染 Ant Design Vue `a-table` 的 Props，例如 `pagination`、`sortDirections`、`expandedRowKeys` 等。对于高级筛选、分组、汇总行等功能，它将通过插槽或子组件的方式进行集成。

2.  **`useTableView.ts`**: 扩展后的 `useTableView` 将负责更全面的 `TableViewConfig` 管理。它将提供方法来更新 `sort`、`filters`、`pagination`、`keyword`、`groupBy`、`aggregations` 等状态，并触发自动保存或手动保存逻辑。它还会处理 `TableViewConfig` 的加载和初始化，包括从后端获取默认视图或用户保存的视图。

3.  **`useTableData.ts`**: 这个新的 Composable 是数据处理的核心。它将观察 `TableViewConfig` 的变化（特别是 `pagination`、`sort`、`filters`、`keyword`、`groupBy` 等字段），并根据这些变化构建 `TableViewQueryParams` 对象。然后，它会调用 `requestApi` prop 传入的后端接口来获取数据。对于前端分页、排序、筛选的场景，它会在获取到所有数据后，在前端进行相应的处理。它还会管理 `loading` 和 `error` 状态，并暴露给 `ProTable`。

4.  **排序与筛选**: `ProTable` 会监听 `a-table` 的 `change` 事件，从中获取排序和筛选的变化。这些变化会被转化为 `TableViewSortConfig[]` 和 `TableViewFilterGroup` 的形式，然后通过 `useTableView` 更新 `TableViewConfig`。`useTableData` 接收到更新后的 `TableViewConfig` 后，会重新请求数据。

5.  **高级筛选面板**: `AdvancedFilterPanel.vue` 将提供一个用户友好的界面来构建复杂的筛选条件。当用户点击“应用”时，它会触发 `apply` 事件，将生成的 `TableViewFilterGroup` 传递给 `ProTable`。`ProTable` 接收到后，会更新 `TableViewConfig` 中的 `filters` 字段，从而触发数据重新加载。

6.  **展开行与树形表格**: `ProTable` 将通过 `a-table` 的 `expandedRowKeys` 和 `childrenColumnName` 等 Props 来支持展开行和树形表格。`TableViewConfig` 中的 `expandedRowKeys` 和 `treeConfig` 将用于控制这些行为，并通过 `useTableView` 进行持久化。

7.  **分组展示与汇总行**: `ProTable` 将根据 `TableViewConfig` 中的 `groupBy` 和 `aggregations` 配置，在渲染 `a-table` 时进行数据预处理或使用自定义渲染器 (`SummaryRow.vue`) 来展示分组和汇总信息。这可能需要对 `a-table` 的 `customRender` 或 `slots` 进行扩展。

8.  **数据高亮/条件格式化**: `ProTable` 将遍历 `TableViewConfig` 中的 `conditionalFormatting` 规则。在渲染每个单元格或行时，会根据规则中的 `condition` 判断是否满足条件，如果满足，则应用 `style` 中定义的样式。这可以通过 `a-table` 的 `customCell` 或 `customRow` 属性结合 `ConditionalFormattingRenderer.vue` 来实现。

9.  **空态/加载态/异常态**: `ProTable` 将根据 `useTableData` 提供的 `loading` 和 `error` 状态，以及 `dataSource` 的长度，动态显示不同的占位符。这可以通过 `a-table` 的 `loading` 属性和 `empty` 插槽来实现，并提供统一的默认样式和可定制的插槽内容。

### 五、与持久化视图的集成

数据查看能力的各项状态（如排序、筛选、分页、全局搜索、分组、展开行、条件格式化等）需要与 `TableViewConfig` 紧密集成，并通过 `useTableView` 组合式函数进行持久化管理。

#### 该维度的状态如何持久化到 `TableViewConfig`

1.  **排序 (`sort`)**: 当用户点击表头进行排序时，`ProTable` 会捕获 `a-table` 的 `change` 事件，解析出当前的排序字段和顺序。这些信息将被转化为 `TableViewSortConfig[]` 数组，并更新 `TableViewConfig.sort` 字段。
2.  **筛选 (`filters`)**: 
    - **列筛选**: 当用户通过列头筛选菜单进行筛选时，筛选条件会被解析为 `TableViewFilterCondition`，并添加到 `TableViewConfig.filters` 中。
    - **高级筛选面板**: `AdvancedFilterPanel.vue` 输出的 `TableViewFilterGroup` 将直接赋值给 `TableViewConfig.filters` 字段。
3.  **分页 (`pagination`)**: 当用户切换页码或改变每页大小时，`ProTable` 会捕获 `a-table` 的 `change` 事件，更新 `TableViewConfig.pagination.current` 和 `TableViewConfig.pagination.pageSize`。
4.  **全局搜索 (`keyword`)**: 全局搜索框的输入值将直接更新 `TableViewConfig.keyword` 字段。
5.  **分组 (`groupBy`)**: 当用户配置分组时，分组字段和顺序将转化为 `TableViewGroupByConfig[]`，并更新 `TableViewConfig.groupBy` 字段。
6.  **展开行 (`expandedRowKeys`)**: `a-table` 的 `expandedRowsChange` 事件将提供当前所有展开行的 `key` 数组，该数组将更新 `TableViewConfig.expandedRowKeys` 字段。
7.  **树形表格 (`treeConfig`)**: 树形表格的展开状态（如 `defaultExpandAll`）和子节点字段名 (`childrenKey`) 将存储在 `TableViewConfig.treeConfig` 中。
8.  **条件格式化 (`conditionalFormatting`)**: 用户在配置界面定义的条件格式化规则将以 `TableViewConditionalFormattingRule[]` 的形式存储在 `TableViewConfig.conditionalFormatting` 字段中。

所有这些状态的更新都将通过 `useTableView` 提供的 `updateTableViewConfig` 或类似方法进行，最终通过 `api-system.ts` 中的 `PUT /table-views/{id}/config` 接口将完整的 `TableViewConfig` 保存到后端。

#### 自动保存 vs 手动保存策略

- **自动保存**: 对于用户频繁操作且不影响数据完整性的状态（如列宽调整、列顺序、密度、分页、排序、筛选），可以采用自动保存策略。`useTableView.ts` 中已有的 `scheduleAutoSave` (400ms 防抖) 机制可以复用，当 `TableViewConfig` 的相关字段发生变化时，触发自动保存。这能提供流畅的用户体验，避免用户忘记保存配置。
- **手动保存**: 对于可能导致数据查询结果显著变化或用户需要明确确认的复杂配置（如高级筛选面板的条件构建、分组配置、条件格式化规则），应提供明确的“保存”按钮，由用户手动触发保存操作。这可以避免用户在调试复杂配置时频繁触发保存，影响性能或造成不必要的后端请求。`TableViewToolbar.vue` 中的“保存”/“另存为”功能将用于此目的。

### 六、低代码（AMIS）集成方案

为了在 AMIS 低代码平台中支持维度3的数据查看能力，我们需要将封装的 Vue 组件或其能力映射到 AMIS Schema，并可能需要注册自定义 AMIS 插件。

#### 如何在 AMIS Schema 中支持该能力

1.  **基础能力（排序、筛选、分页、全局搜索）**: AMIS 的 `Table` 组件本身就支持这些基础能力。我们可以通过扩展 `DynamicTableCrudPage.vue` 中动态生成的 AMIS Schema，将 `TableViewConfig` 中的 `sort`、`filters`、`pagination`、`keyword` 等信息映射到 AMIS `Table` 组件的相应属性上。例如，将 `TableViewConfig.sort` 转换为 AMIS `Table` 的 `orderBy` 和 `orderDir` 属性，将 `TableViewConfig.filters` 转换为 AMIS `Table` 的 `filter` 属性。
2.  **高级筛选面板**: AMIS 提供了 `CRUD` 组件的 `filter` 属性，可以配置一个表单作为筛选器。我们可以将 `AdvancedFilterPanel.vue` 封装成一个 AMIS 自定义组件，并在 `DynamicTableCrudPage.vue` 生成的 Schema 中引用它。当 `AdvancedFilterPanel` 输出 `TableViewFilterGroup` 时，需要一个机制将其转换为 AMIS `filter` 组件能够理解的查询参数格式。
3.  **展开行/树形表格**: AMIS `Table` 组件支持 `expandable` 和 `childrenColumnName` 属性。我们可以将 `TableViewConfig` 中的 `expandedRowKeys` 和 `treeConfig` 映射到这些属性。对于展开行的内容，可以通过 AMIS 的 `rowActions` 或 `itemActions` 结合自定义组件来实现。
4.  **分组展示/汇总行**: AMIS `Table` 组件对分组和汇总行的支持相对有限。可能需要将 `ProTable.vue` 作为一个整体的 AMIS 自定义组件进行封装，或者通过 AMIS 的 `columns` 属性中的 `groupName` 和 `summary` 属性进行有限支持。如果需要更高级的自定义，可能需要开发自定义渲染器。
5.  **数据高亮/条件格式化**: AMIS `Table` 组件的 `columns` 属性中的 `className` 或 `bodyClassName` 可以通过表达式动态设置。我们可以将 `TableViewConditionalFormattingRule` 转换为 AMIS 表达式，在运行时根据数据判断并应用样式。例如，`${data.status === 'error' ? 'text-danger' : ''}`。更复杂的条件格式化可能需要自定义列渲染器。

#### 是否需要注册自定义 AMIS 插件

对于以下情况，可能需要注册自定义 AMIS 插件：

- **高级筛选面板**: 如果 `AdvancedFilterPanel.vue` 的功能复杂且难以通过 AMIS 现有组件属性映射，可以将其封装为自定义 AMIS 组件，并注册为插件。这样可以在 AMIS 编辑器中以拖拽或配置的方式使用。
- **ProTable 整体封装**: 如果希望在 AMIS 中直接使用 `ProTable.vue` 提供的所有高级数据查看能力，可以将其作为一个整体的自定义 AMIS 组件进行注册。这样，AMIS Schema 中可以直接配置 `ProTable` 的 `tableKey`、`columns` 等属性，并由 `ProTable` 内部处理所有逻辑。
- **复杂分组/汇总行渲染**: 如果 AMIS 现有能力无法满足复杂的分组展示和汇总行渲染需求，可能需要开发自定义渲染器插件来扩展 AMIS 的表格渲染能力。

注册自定义 AMIS 插件通常涉及编写一个 React 组件（因为 AMIS 基于 React），并在 `business-plugins.ts` 中进行注册。这个 React 组件将作为 Vue 组件的包装器，负责将 AMIS Schema 的配置转换为 Vue 组件的 Props，并将 Vue 组件的 Emits 转换为 AMIS 的事件。

### 七、优先级与实施建议

#### 基础版/进阶版/高阶版分级

为了逐步实现维度3的各项能力，建议按照以下分级进行实施：

**基础版 (P0)**: 核心且高频使用的功能，应优先实现。
- **排序**: 单列排序（服务端）。
- **筛选**: 文本、枚举、时间、数字的基础列筛选（服务端）。
- **全局搜索**: 关键词搜索（服务端）。
- **分页**: 服务端分页。
- **空态/加载态/异常态**: 统一的视觉反馈。
- **展开行**: 基础展开行功能。
- **与持久化视图集成**: 排序、筛选、分页、全局搜索状态的自动保存。

**进阶版 (P1)**: 提升用户体验和满足更复杂业务需求的功能。
- **排序**: 多列排序（服务端）。
- **高级筛选面板**: 可视化条件构建器，支持 AND/OR 逻辑和条件组嵌套。
- **树形表格**: 基础树形表格功能。
- **汇总行/合计**: 基础的汇总行功能（如总和、平均值）。
- **前端分页/排序/筛选**: 针对小数据量场景的优化。
- **与持久化视图集成**: 高级筛选面板、树形表格、汇总行状态的手动保存。

**高阶版 (P2)**: 满足个性化和高级定制需求的功能。
- **分组展示**: 灵活的分组配置和展示。
- **数据高亮/条件格式化**: 强大的条件样式设置。
- **更复杂的汇总行**: 支持自定义聚合函数和格式化。
- **低代码 AMIS 深度集成**: 自定义 AMIS 插件，实现 `ProTable` 的整体集成或复杂组件的映射。

#### 预估工作量（人天）

以下是各项能力的粗略预估工作量，实际工作量可能因具体实现细节和团队效率而异：

| 功能模块             | 预估人天 | 备注                                   |
| :------------------- | :------- | :------------------------------------- |
| **基础版**           |          |                                        |
| 单列排序             | 1        | 改造 `useTableView` 和 `ProTable`      |
| 基础列筛选           | 2        | 改造 `useTableView` 和 `ProTable`      |
| 全局搜索             | 1        | 改造 `useTableView` 和 `ProTable`      |
| 服务端分页           | 1        | 改造 `useTableView` 和 `ProTable`      |
| 空/加载/异常态       | 1        | `ProTable` 统一处理                    |
| 展开行               | 1        | `ProTable` 封装                        |
| 基础持久化集成       | 2        | `useTableView` 自动保存机制            |
| **小计**             | **9**    |                                        |
| **进阶版**           |          |                                        |
| 多列排序             | 2        | 改造 `useTableView` 和 `ProTable`      |
| 高级筛选面板         | 5        | 新增 `AdvancedFilterPanel` 组件        |
| 树形表格             | 3        | `ProTable` 封装                        |
| 汇总行/合计          | 3        | `ProTable` 封装，新增 `SummaryRow`     |
| 前端分页/排序/筛选   | 3        | `useTableData` 逻辑调整                |
| 进阶持久化集成       | 2        | `useTableView` 手动保存机制            |
| **小计**             | **18**   |                                        |
| **高阶版**           |          |                                        |
| 分组展示             | 4        | `ProTable` 封装，数据处理复杂          |
| 数据高亮/条件格式化  | 4        | `ProTable` 封装，新增渲染器            |
| 复杂汇总行           | 2        | 扩展 `SummaryRow`                      |
| AMIS 深度集成        | 5        | 自定义插件开发，React/Vue 桥接         |
| **小计**             | **15**   |                                        |
| **总计**             | **42**   |                                        |

**维度3总预估人天：20-30人天** (考虑到P0和P1的优先级，P2可延后)

#### 关键风险与注意事项

1.  **性能问题**: 复杂筛选、多列排序、分组、条件格式化等功能可能对前端渲染性能和后端查询性能造成影响。需要进行充分的性能测试和优化，特别是对于大数据量表格。
2.  **后端接口支持**: 确保后端 API 能够支持前端提出的所有查询参数（如多列排序、复杂筛选条件、分组聚合等）。需要与后端团队紧密协作，定义清晰的接口规范。
3.  **AMIS 集成复杂度**: AMIS 低代码平台的集成可能涉及 React 和 Vue 之间的组件通信和数据转换，这可能增加开发复杂度和调试难度。需要投入额外精力进行技术预研和方案设计。
4.  **用户体验**: 高级功能（如高级筛选面板、条件格式化）的 UI/UX 设计需要简洁直观，避免用户学习成本过高。需要与产品和设计团队紧密合作，进行用户测试和迭代。
5.  **类型安全**: 随着数据模型的复杂化，确保 TypeScript 类型定义的准确性和完整性至关重要，以避免运行时错误。
6.  **可扩展性**: 封装方案应具备良好的可扩展性，方便未来新增数据查看能力或调整现有能力。例如，筛选操作符、聚合函数等应易于扩展。
7.  **国际化**: 所有新增的 UI 文本和提示信息都需要支持国际化。
8.  **测试覆盖**: 对所有新增和改造的功能进行充分的单元测试、集成测试和端到端测试，确保功能的稳定性和正确性。


---

## 维度 4：行操作与选择能力

- **现状评估**：缺失
- **实施优先级**：P0
- **预估人天**：20 天

### 一、现状分析
- **代码库中已有哪些相关实现（文件路径 + 功能说明）**
  - 现有代码库中，与“行操作与选择能力”直接相关的通用封装实现基本缺失。`composables/useTableView.ts` 主要关注列配置、视图持久化等，`composables/useCrudPage.ts` 侧重于通用CRUD操作，均未涉及表格行级别的选择、操作按钮、展开或拖拽等功能。
  - `components/table/editable-cell.vue` 实现了单元格级别的编辑功能，但与行操作的范畴不同。
  - `types/api.ts` 和 `types/dynamic-tables.ts` 中定义的 `TableViewConfig` 等类型，目前也未包含行选择或行操作相关的配置项。

- **存在哪些缺口和不足**
  - **行选择与批量操作**：代码库摘要明确指出“行选择与批量操作 - 未封装”。这意味着单选、多选、跨页选择、全选反选、选择禁用等核心选择逻辑和UI交互均未实现。
  - **行内操作按钮**：缺乏统一的行内操作按钮（如编辑、删除、查看详情等）的配置和渲染机制。
  - **批量操作栏**：与行选择紧密相关的批量操作栏（显示已选数量、提供批量操作按钮）未实现。
  - **右键菜单**：表格行的右键菜单功能未实现，这在某些高级交互场景下是重要的。
  - **行拖拽排序**：代码库摘要明确指出“行拖拽排序 - 未实现”。
  - **行展开/行详情抽屉**：代码库摘要明确指出“展开行 - 未封装”。行展开和行详情抽屉（或弹窗）是展示行额外信息的两种常见方式，目前均未实现。
  - **行点击hover/行状态标记**：基础的行交互（如hover效果）和行状态标记（如高亮、禁用样式）也未有统一的封装。

### 二、封装目标
- **该维度需要封装哪些核心能力（对照用户需求清单逐条说明）**
  - **单选多选**：支持表格行的单选和多选功能，提供配置项控制选择模式。
  - **跨页选择**：在多选模式下，支持用户在不同分页之间保持选择状态，实现跨页选择。
  - **全选反选**：提供全选和反选功能，方便用户快速选择或取消选择所有可见行。
  - **选择禁用**：支持根据业务逻辑动态禁用某些行的选择能力。
  - **行点击hover**：实现表格行在鼠标悬停时的视觉反馈（hover效果）。
  - **行内操作按钮**：提供灵活的配置机制，允许在每行末尾渲染自定义操作按钮组，支持按钮的权限控制、文本/图标显示、点击事件等。
  - **批量操作栏**：当有行被选中时，在表格上方或下方显示一个批量操作栏，展示已选数量，并提供可配置的批量操作按钮。
  - **右键菜单**：为表格行提供可配置的右键上下文菜单，支持自定义菜单项和点击事件。
  - **行拖拽排序**：实现表格行的拖拽排序功能，支持在同一页内或跨页（如果结合后端API）进行排序。
  - **行展开**：支持表格行的展开功能，展开后可显示该行的更多详细信息或嵌套表格。
  - **行详情抽屉**：提供一种机制，当用户点击行详情按钮或特定区域时，从侧边滑出抽屉（Drawer）展示该行的详细信息。
  - **行状态标记**：支持根据业务数据或状态，对表格行进行视觉标记（如背景色、边框、图标等），以区分不同状态的行（如禁用、警告、成功等）。

- **普通面板场景 vs 低代码场景的差异处理**
  - **普通面板场景**：
    - 封装为独立的Vue组件或Composable，提供丰富的Props和Slots，允许开发者通过编码方式灵活配置和扩展。
    - 强调TypeScript类型安全和开发体验，提供详细的文档和示例。
    - 核心逻辑和UI渲染由封装组件负责，开发者通过传入数据和事件回调进行交互。
  - **低代码场景（AMIS）**：
    - 需要将上述能力映射到AMIS Schema的属性上，使得用户可以通过JSON配置来启用和定制行操作与选择。
    - 对于复杂交互（如行拖拽、右键菜单），可能需要注册自定义AMIS组件或插件，将Vue组件桥接到AMIS运行时。
    - 尽量复用普通面板场景下的核心逻辑和数据模型，减少重复开发。

### 三、核心数据模型 / 类型定义
为了支持上述能力，需要在 `types/api.ts` 或新增 `types/table-row-features.ts` 中定义以下核心类型：

```typescript
// types/table-row-features.ts (新增文件)

// 行选择配置
export type TableRowSelectionType = 'checkbox' | 'radio' | false;

export interface TableRowSelectionConfig {
  type?: TableRowSelectionType; // 选择类型：多选/单选/禁用
  selectedRowKeys?: (string | number)[]; // 已选中的行key集合
  onSelect?: (record: any, selected: boolean, selectedRows: any[], nativeEvent: Event) => void; // 单行选择回调
  onSelectAll?: (selected: boolean, selectedRows: any[], changeRows: any[]) => void; // 全选/反选回调
  getCheckboxProps?: (record: any) => { disabled?: boolean; name?: string; }; // 配置选择框属性，用于禁用特定行
  preserveSelectedRowKeys?: boolean; // 是否保留跨页选择，默认为true
  showBatchOperations?: boolean; // 是否显示批量操作栏
}

// 行操作按钮配置
export interface TableRowAction {
  key: string; // 唯一标识
  label: string; // 按钮文本
  icon?: string; // 按钮图标 (Ant Design Vue Icon name)
  type?: 'primary' | 'default' | 'danger' | 'link'; // 按钮类型
  onClick: (record: any) => void; // 点击事件回调
  authCode?: string; // 权限码，用于控制按钮显示
  visible?: (record: any) => boolean; // 动态控制按钮可见性
  disabled?: (record: any) => boolean; // 动态控制按钮禁用状态
  confirm?: { // 确认弹窗配置
    title: string;
    content: string;
    onConfirm: (record: any) => Promise<any>;
  };
}

// 行右键菜单配置
export interface TableRowContextMenuItem {
  key: string;
  label: string;
  icon?: string;
  onClick: (record: any) => void;
  authCode?: string;
  visible?: (record: any) => boolean;
  disabled?: (record: any) => boolean;
  divider?: boolean; // 是否显示分割线
}

// 行展开配置
export interface TableRowExpandConfig {
  expandedRowKeys?: (string | number)[]; // 已展开的行key集合
  onExpand?: (expanded: boolean, record: any) => void; // 展开/收起回调
  expandedRowRender?: (record: any, index: number, indent: number, expanded: boolean) => VNode | JSX.Element; // 展开行渲染函数
  rowExpandable?: (record: any) => boolean; // 判断行是否可展开
  expandIconColumnIndex?: number; // 展开图标列索引
}

// 行拖拽排序配置
export interface TableRowDragSortConfig {
  onSortEnd: (oldIndex: number, newIndex: number, records: any[]) => Promise<any>; // 排序结束回调
  dragHandleSelector?: string; // 拖拽手柄选择器
  disabled?: boolean; // 是否禁用拖拽
}

// 行状态标记配置
export interface TableRowStatusConfig {
  rowClassName?: (record: any, index: number) => string; // 根据记录返回行class
  rowStyle?: (record: any, index: number) => CSSProperties; // 根据记录返回行style
}

// 批量操作栏配置
export interface TableBatchOperationConfig {
  actions: TableRowAction[]; // 批量操作按钮列表
  onClearSelection?: () => void; // 清除选择回调
  visible?: boolean; // 是否可见
}

// TableViewConfig 扩展 (types/api.ts)
declare module './api' {
  interface TableViewConfig {
    rowSelection?: TableRowSelectionConfig; // 行选择配置
    rowActions?: TableRowAction[]; // 行内操作按钮配置
    rowContextMenu?: TableRowContextMenuItem[]; // 行右键菜单配置
    rowExpand?: TableRowExpandConfig; // 行展开配置
    rowDragSort?: TableRowDragSortConfig; // 行拖拽排序配置
    rowStatus?: TableRowStatusConfig; // 行状态标记配置
    batchOperations?: TableBatchOperationConfig; // 批量操作栏配置
  }
}
```

- **说明与现有 TableViewConfig 的集成方式**
  - 在 `types/api.ts` 中通过模块扩展（`declare module './api'`）的方式，为 `TableViewConfig` 接口新增 `rowSelection`, `rowActions`, `rowContextMenu`, `rowExpand`, `rowDragSort`, `rowStatus`, `batchOperations` 等属性。
  - 这种方式可以平滑地将行操作与选择相关的配置集成到现有的视图配置体系中，使得这些能力可以随视图一起保存和加载。
  - `selectedRowKeys` 和 `expandedRowKeys` 等状态可以在 `TableViewConfig` 中持久化，实现视图切换时选择和展开状态的恢复。

### 四、组件/Composable 封装方案
- **新增或改造哪些文件（给出文件路径建议）**
  - **新增 Composable**：
    - `composables/useTableRowSelection.ts`：封装行选择的核心逻辑，包括单选、多选、跨页选择、全选反选、选择禁用等。
    - `composables/useTableRowActions.ts`：封装行内操作按钮、右键菜单的渲染和事件处理逻辑。
    - `composables/useTableRowExpand.ts`：封装行展开/收起、行详情抽屉的逻辑。
    - `composables/useTableRowDragSort.ts`：封装行拖拽排序的逻辑（可能需要引入第三方库如 `vue-draggable-next` 或 `sortablejs`）。
  - **改造组件**：
    - `components/table/TableView.vue` (或类似的核心表格组件)：作为 Ant Design Vue `a-table` 的二次封装，集成上述 Composable，并提供统一的 Props 接口。
    - `components/table/TableRowActions.vue` (新增组件)：用于渲染行内操作按钮。
    - `components/table/TableBatchOperations.vue` (新增组件)：用于渲染批量操作栏。
    - `components/table/TableRowContextMenu.vue` (新增组件)：用于渲染行右键菜单。
  - **新增类型定义文件**：
    - `types/table-row-features.ts`：如第三节所述，定义行操作与选择相关的类型。

- **关键 Props/Emits/Expose 设计（代码块）**
  以 `TableView.vue` (核心表格组件) 为例，其 Props 应该包含对 `TableViewConfig` 的扩展：

  ```typescript
  // components/table/TableView.vue (部分关键Props设计)
  import { TableRowSelectionConfig, TableRowAction, TableRowContextMenuItem, TableRowExpandConfig, TableRowDragSortConfig, TableRowStatusConfig, TableBatchOperationConfig } from '@/types/table-row-features';
  import { TableViewConfig, TableViewColumn } from '@/types/api';

  interface TableViewProps {
    // ... 现有Props (如 dataSource, columns, pagination, loading 等)
    tableKey: string; // 用于视图持久化的唯一键
    columns: TableViewColumn[]; // 列配置
    tableViewConfig?: TableViewConfig; // 完整的视图配置，包含行操作与选择配置

    // 行选择
    rowSelection?: TableRowSelectionConfig; // 行选择配置
    selectedRowKeys?: (string | number)[]; // 外部控制已选中的行key
    'onUpdate:selectedRowKeys'?: (keys: (string | number)[]) => void; // 更新选中行key事件

    // 行操作
    rowActions?: TableRowAction[]; // 行内操作按钮配置
    rowContextMenu?: TableRowContextMenuItem[]; // 行右键菜单配置

    // 行展开
    rowExpand?: TableRowExpandConfig; // 行展开配置
    expandedRowKeys?: (string | number)[]; // 外部控制已展开的行key
    'onUpdate:expandedRowKeys'?: (keys: (string | number)[]) => void; // 更新展开行key事件

    // 行拖拽排序
    rowDragSort?: TableRowDragSortConfig; // 行拖拽排序配置

    // 行状态标记
    rowStatus?: TableRowStatusConfig; // 行状态标记配置

    // 批量操作栏
    batchOperations?: TableBatchOperationConfig; // 批量操作栏配置
  }

  // Emits
  interface TableViewEmits {
    (e: 'row-click', record: any, index: number, event: Event): void;
    (e: 'row-hover', record: any, index: number, event: Event): void;
    (e: 'update:selectedRowKeys', keys: (string | number)[]): void;
    (e: 'update:expandedRowKeys', keys: (string | number)[]): void;
    (e: 'row-drag-sort-end', oldIndex: number, newIndex: number, records: any[]): void;
    // ... 其他现有Emits
  }

  // Expose (示例：提供清除选择的方法)
  interface TableViewExpose {
    clearSelection: () => void;
    // ... 其他需要暴露的方法
  }
  ```

- **核心实现逻辑说明**
  1.  **行选择**：
      - 在 `useTableRowSelection.ts` 中，管理 `selectedRowKeys` 状态，并提供 `onSelect`, `onSelectAll` 等事件处理函数。
      - 利用 Ant Design Vue `a-table` 的 `rowSelection` Prop，将封装的逻辑和状态传递给底层表格组件。
      - 实现 `getCheckboxProps` 逻辑，根据 `record` 动态禁用选择框。
      - 跨页选择需要将 `selectedRowKeys` 存储在全局状态（如 Pinia）或 `TableViewConfig` 中，并在数据加载时进行合并处理。
      - 批量操作栏的显示/隐藏由 `selectedRowKeys` 的长度决定，并提供清除选择功能。
  2.  **行内操作按钮**：
      - 在 `TableView.vue` 中，通过 `rowActions` Prop 接收操作按钮配置。
      - 在表格的最后一列（或可配置的列）渲染一个 `Action` 列，内部使用 `TableRowActions.vue` 组件遍历 `rowActions` 数组，渲染 `a-button` 或 `a-dropdown`。
      - 按钮的 `visible` 和 `disabled` 属性通过传入 `record` 动态计算。
      - 权限控制通过 `authCode` 结合全局权限管理实现。
  3.  **右键菜单**：
      - 监听表格行的 `contextmenu` 事件，阻止默认行为。
      - 根据 `rowContextMenu` 配置，动态渲染 `TableRowContextMenu.vue` 组件（一个 `a-dropdown` 或自定义浮层）。
      - 菜单项的可见性和禁用状态同样通过函数动态判断。
  4.  **行拖拽排序**：
      - `useTableRowDragSort.ts` 负责集成第三方拖拽库（如 `vue-draggable-next`）。
      - 监听拖拽事件，在 `onSortEnd` 回调中触发 `row-drag-sort-end` 事件，将新的排序结果（`oldIndex`, `newIndex`, `records`）传递给父组件处理（通常是调用后端API更新排序）。
      - 需要处理好拖拽时的视觉反馈（占位符、拖拽样式）。
  5.  **行展开/行详情抽屉**：
      - `useTableRowExpand.ts` 管理 `expandedRowKeys` 状态。
      - 利用 `a-table` 的 `expandedRowRender` 和 `rowExpandable` Prop 实现行展开。
      - 行详情抽屉可以通过点击行内操作按钮触发，使用 Ant Design Vue 的 `a-drawer` 组件展示详情内容。
  6.  **行点击hover/行状态标记**：
      - `a-table` 默认支持 `hover` 效果。
      - 行状态标记通过 `rowStatus.rowClassName` 或 `rowStatus.rowStyle` Prop，动态为行添加CSS类或样式。

### 五、与持久化视图的集成
- **该维度的状态如何持久化到 TableViewConfig**
  - **行选择状态**：`selectedRowKeys` 可以作为 `TableViewConfig.rowSelection.selectedRowKeys` 存储。然而，考虑到 `selectedRowKeys` 通常是临时的用户操作，且可能涉及大量数据，将其持久化到 `TableViewConfig` 中可能不是最佳实践。更合理的做法是，`TableViewConfig` 只存储 `rowSelection.type` 和 `rowSelection.preserveSelectedRowKeys` 等配置，而 `selectedRowKeys` 在每次加载视图时清空或从外部传入。
  - **行展开状态**：`expandedRowKeys` 可以作为 `TableViewConfig.rowExpand.expandedRowKeys` 存储。这对于用户希望在下次打开时保持某些行展开状态的场景很有用。
  - **行操作按钮/右键菜单/批量操作栏**：这些配置（`rowActions`, `rowContextMenu`, `batchOperations`）本身就是 `TableViewConfig` 的一部分，可以直接持久化。它们定义了表格的行为和UI，是视图的重要组成部分。
  - **行拖拽排序**：`rowDragSort` 配置（如 `disabled`）可以持久化。但拖拽后的排序结果通常需要通过后端API更新数据源，而不是直接存储在 `TableViewConfig` 中。
  - **行状态标记**：`rowStatus` 配置（如 `rowClassName` 或 `rowStyle` 的函数定义）不适合直接持久化，因为函数无法序列化。可以考虑持久化一个“状态规则ID”或“状态规则名称”，然后前端根据ID/名称加载预定义的状态规则。

- **自动保存 vs 手动保存策略**
  - **自动保存**：
    - 对于 `rowSelection.type`、`rowExpand.expandedRowKeys`、`rowActions` 等配置项，可以集成到 `useTableView.ts` 的 `scheduleAutoSave` 机制中。
    - 当这些配置发生变化时，通过防抖机制触发 `updateTableViewConfig` API 调用，实现自动保存。
    - 优点：用户体验好，无需手动保存。
    - 缺点：频繁的保存操作可能增加后端压力，且对于 `selectedRowKeys` 这种临时状态，自动保存可能不合适。
  - **手动保存**：
    - 对于 `selectedRowKeys` 等临时性、用户操作频繁且不希望持久化的状态，应采用手动保存策略（即不保存）。
    - 对于 `rowActions` 等配置，如果用户希望明确控制何时保存，也可以提供手动保存按钮。
    - 优点：减少后端压力，用户对保存行为有明确预期。
    - 缺点：用户可能忘记保存，导致配置丢失。
  - **建议**：
    - `rowSelection.type`、`rowActions`、`rowContextMenu`、`rowExpand` 的基本配置（非动态状态）应支持自动保存。
    - `expandedRowKeys` 可以选择性地支持自动保存，或在视图加载时提供一个“恢复上次展开状态”的选项。
    - `selectedRowKeys` 不应自动保存，而是在每次加载视图时重置。
    - `rowDragSort` 的 `onSortEnd` 回调应直接触发业务数据的更新，而不是通过 `TableViewConfig` 持久化。

### 六、低代码（AMIS）集成方案
- **如何在 AMIS Schema 中支持该能力**
  - **行选择**：
    - AMIS 的 `table` 组件本身支持 `rowSelection` 属性，可以配置 `type` (checkbox/radio)、`selected` (已选key)、`selectable` (禁用函数) 等。可以直接映射 `TableRowSelectionConfig` 中的大部分属性。
    - 跨页选择需要 AMIS `table` 组件支持 `keepItemSelectionText` 或类似属性，或者通过自定义数据源和状态管理实现。
  - **行内操作按钮**：
    - AMIS `table` 组件的 `columns` 中可以定义 `type: 'operation'` 的列，内部包含 `buttons` 数组，每个按钮可以配置 `label`, `icon`, `actionType`, `level`, `visibleOn` (条件显示) 等。`TableRowAction` 可以很好地映射到这里。
    - 权限控制可以通过 `visibleOn` 结合 AMIS 的 `data` 表达式实现。
  - **批量操作栏**：
    - AMIS `table` 组件支持 `bulkActions` 属性，用于定义批量操作按钮。当有行被选中时，会自动显示。
  - **右键菜单**：
    - AMIS `table` 组件默认不支持行右键菜单。需要通过自定义组件或插件实现。
  - **行拖拽排序**：
    - AMIS `table` 组件支持 `draggable` 属性，开启后可以进行行拖拽排序。`onSortEnd` 逻辑需要通过 AMIS 的 `onEvent` 或自定义 `actionType` 来触发后端API调用。
  - **行展开/行详情抽屉**：
    - AMIS `table` 组件支持 `expandable` 属性，可以配置 `expandedRowRender` (渲染展开内容) 和 `rowExpandable` (判断是否可展开)。
    - 行详情抽屉可以通过行内操作按钮触发一个 `drawer` 类型的 `action`，在 `drawer` 中配置详情页面的 Schema。
  - **行状态标记**：
    - AMIS `table` 组件的 `rowClassNameExpr` 属性可以根据行数据动态返回CSS类名，实现行状态标记。

- **是否需要注册自定义 AMIS 插件**
  - **可能需要**：
    - **右键菜单**：AMIS 默认不提供行右键菜单功能，需要注册自定义 AMIS 组件或插件来实现。该插件可以封装 `TableRowContextMenu.vue` 组件，并通过 AMIS 的 `renderer` 机制将其集成到 `table` 行中。
    - **复杂交互的桥接**：对于一些 Ant Design Vue 特有的复杂交互（如拖拽库的深度集成），如果 AMIS 自身提供的 `draggable` 属性无法满足需求，可能需要自定义插件来桥接 `useTableRowDragSort.ts` 封装的 Vue 逻辑。
    - **统一的权限控制**：虽然 AMIS 有 `visibleOn`，但如果需要与 SecurityPlatform 自身的权限体系（如 `authCode`）深度集成，可能需要一个自定义的 AMIS 插件来提供统一的权限判断逻辑。
  - **不需要**：
    - 对于行选择、行内操作按钮、批量操作栏、行展开、行状态标记等，AMIS `table` 组件已提供较为完善的属性支持，可以直接通过 Schema 配置实现，无需额外插件。

### 七、优先级与实施建议
- **基础版/进阶版/高阶版分级**
  - **基础版 (P0)**：
    - **行选择**：单选、多选、全选反选、选择禁用。
    - **行内操作按钮**：支持配置按钮文本、图标、点击事件、权限控制。
    - **批量操作栏**：显示已选数量，提供批量操作按钮。
    - **行点击hover**：基础hover效果。
    - **行状态标记**：通过 `rowClassName` 或 `rowStyle` 实现。
  - **进阶版 (P1)**：
    - **跨页选择**：在多选模式下，支持跨页保持选择状态。
    - **行展开**：支持行展开，显示额外信息。
    - **行详情抽屉**：通过行内操作按钮触发侧边抽屉显示详情。
    - **行拖拽排序**：实现行拖拽排序功能（单页）。
  - **高阶版 (P2)**：
    - **右键菜单**：提供可配置的行右键上下文菜单。
    - **复杂拖拽排序**：支持跨页拖拽排序（需后端支持）。
    - **更精细的行状态管理**：例如，行内特定区域的状态标记、闪烁效果等。

- **预估工作量（人天）**
  - **基础版 (P0)**：约 5-8 人天。
    - 行选择与批量操作：3-4 人天。
    - 行内操作按钮与行状态标记：2-3 人天。
  - **进阶版 (P1)**：约 7-10 人天。
    - 跨页选择：2-3 人天。
    - 行展开与详情抽屉：3-4 人天。
    - 行拖拽排序：2-3 人天。
  - **高阶版 (P2)**：约 4-6 人天。
    - 右键菜单：2-3 人天。
    - 复杂拖拽排序及其他：2-3 人天。
  - **总计预估**：约 16-24 人天。

- **关键风险与注意事项**
  - **性能问题**：
    - 大量数据下的跨页选择和行展开可能导致性能问题，需要优化数据结构和渲染机制（如虚拟滚动结合）。
    - 频繁的 `selectedRowKeys` 或 `expandedRowKeys` 更新可能触发不必要的组件重渲染，需合理使用 `memoization` 或 `shallowRef`。
  - **状态管理复杂性**：
    - 跨页选择和展开状态需要在全局或父组件中进行管理，确保状态的一致性。
    - `TableViewConfig` 的持久化需要考虑数据量和序列化/反序列化的问题。
  - **第三方库集成**：
    - 行拖拽排序可能需要引入第三方库，需要评估其稳定性、兼容性和维护成本。
  - **低代码AMIS兼容性**：
    - AMIS 对某些高级交互（如右键菜单）的支持可能不完善，需要投入额外精力开发自定义插件，并确保与 AMIS 运行时环境的兼容性。
    - AMIS 的表达式语言在处理复杂逻辑（如动态禁用、权限判断）时可能不如 TypeScript 灵活，需要权衡。
  - **权限控制**：
    - 行内操作按钮和右键菜单的权限控制需要与 SecurityPlatform 现有的权限体系（如 `authCode`）紧密结合，确保安全性和一致性。
  - **用户体验**：
    - 拖拽排序、右键菜单等高级交互需要良好的用户引导和视觉反馈，避免用户困惑。
    - 批量操作的确认机制（如二次确认弹窗）需要设计完善。

---

---

## 维度 5：编辑能力

- **现状评估**：部分
- **实施优先级**：P0
- **预估人天**：12 天

### 一、现状分析

- **代码库中已有哪些相关实现**：
  - `components/table/editable-cell.vue`：已实现基础的单元格编辑功能，支持 `text`、`number`、`select` 类型，具备点击编辑、ESC取消、Enter保存、异步保存回调以及 `editing`/`saving` 状态管理。
  - `types/dynamic-tables.ts`：定义了 `DynamicColumnDef`，其中包含 `quickEdit` 属性，为动态表提供了快速编辑的配置基础。
  - `types/dynamic-tables.ts`：定义了 `DynamicFieldPermissionRule`，包含 `canEdit` 属性，支持基于角色的字段级编辑权限控制。

- **存在哪些缺口和不足**：
  - **编辑模式单一**：目前仅支持单元格级别的编辑，缺乏行编辑、批量编辑的完整支持。
  - **交互能力缺失**：不支持新增行、删除行、复制粘贴、回车切换焦点等高级交互。
  - **校验机制不完善**：缺乏统一的编辑校验、联动校验和必填格式校验机制。
  - **状态管理不足**：缺少编辑态与只读态的全局切换、修改高亮、撤销重做、脏数据提示、保存取消以及乐观更新回滚等复杂状态管理能力。

### 二、封装目标

- **核心能力封装**：
  1. **编辑模式支持**：扩展支持单元格编辑、行编辑、批量编辑模式。
  2. **高级交互**：实现新增行、删除行、复制粘贴（支持与Excel互通）、回车/Tab键切换单元格焦点。
  3. **校验机制**：内置必填校验、格式校验，支持自定义异步校验和跨字段联动校验。
  4. **状态与提示**：支持全局编辑态/只读态切换，修改单元格高亮显示，脏数据拦截提示。
  5. **操作回溯**：实现撤销/重做（Undo/Redo）功能。
  6. **数据提交**：支持统一的保存/取消操作，实现乐观更新并在失败时自动回滚。

- **普通面板场景 vs 低代码场景的差异处理**：
  - **普通面板场景**：通过 Vue 组件（如 `EditableTable`）和 Composable（如 `useTableEdit`）提供编程式 API，开发者手动配置列的编辑类型、校验规则和保存逻辑。
  - **低代码场景（AMIS）**：通过 AMIS Schema 配置 `quickEdit`，利用 AMIS 内置的表单项组件进行渲染，状态管理和提交逻辑由 AMIS 引擎接管，前端需提供适配器将 AMIS 的编辑事件与后端的保存接口对接。

### 三、核心数据模型 / 类型定义

```typescript
// types/table-edit.ts
export type EditMode = 'cell' | 'row' | 'batch';

export interface EditRule {
  required?: boolean;
  pattern?: RegExp;
  message?: string;
  validator?: (value: any, record: any) => Promise<boolean | string>;
}

export interface EditableColumnConfig {
  type: 'text' | 'number' | 'select' | 'date' | 'boolean';
  options?: any[]; // For select
  rules?: EditRule[];
  dependencies?: string[]; // 联动校验依赖的字段
}

export interface EditRecordState {
  isEditing: boolean;
  isNew: boolean;
  isDeleted: boolean;
  isDirty: boolean;
  originalData: any;
  currentData: any;
  errors: Record<string, string>;
}

export interface TableEditState {
  mode: EditMode;
  globalEditing: boolean;
  records: Record<string | number, EditRecordState>;
  history: {
    past: any[];
    future: any[];
  };
}
```

**与现有 TableViewConfig 的集成方式**：
在 `TableViewColumnConfig` 中扩展 `editConfig?: EditableColumnConfig` 属性。编辑状态（如脏数据、历史记录）属于运行时状态，不持久化到 `TableViewConfig` 中，但编辑模式（`mode`）可以作为视图配置的一部分进行保存。

### 四、组件/Composable 封装方案

- **新增/改造文件**：
  - 新增 `composables/useTableEdit.ts`：核心状态管理与逻辑实现。
  - 改造 `components/table/editable-cell.vue`：支持更多类型和联动校验。
  - 新增 `components/table/editable-row.vue`：行编辑容器。
  - 新增 `components/table/table-edit-toolbar.vue`：提供保存、取消、撤销、重做按钮。

- **关键 Props/Emits/Expose 设计**：

```typescript
// composables/useTableEdit.ts
export function useTableEdit(options: {
  mode: EditMode;
  rowKey: string;
  onSave?: (records: any[]) => Promise<void>;
}) {
  const state = reactive<TableEditState>({ /* ... */ });
  
  const startEdit = (rowKey: string | number, columnKey?: string) => { /* ... */ };
  const cancelEdit = (rowKey?: string | number) => { /* ... */ };
  const saveEdit = async () => { /* 乐观更新与回滚逻辑 */ };
  const validate = async (rowKey: string | number) => { /* ... */ };
  const undo = () => { /* ... */ };
  const redo = () => { /* ... */ };
  
  return {
    state,
    startEdit,
    cancelEdit,
    saveEdit,
    validate,
    undo,
    redo
  };
}
```

- **核心实现逻辑说明**：
  - **状态管理**：使用 `reactive` 维护每个记录的编辑状态（`EditRecordState`），通过 `originalData` 和 `currentData` 的对比判断是否为脏数据（`isDirty`）。
  - **撤销重做**：每次修改数据时，将当前状态的深拷贝推入 `history.past` 栈，清空 `history.future` 栈。
  - **乐观更新**：在调用 `onSave` 前，先将 UI 状态更新为保存成功，若 `onSave` 抛出异常，则利用 `originalData` 进行回滚，并提示错误。
  - **键盘导航**：在 `editable-cell.vue` 中监听 `keydown` 事件，拦截 Enter/Tab 键，触发相邻单元格的 `startEdit`。

### 五、与持久化视图的集成

- **持久化策略**：
  - 运行时的编辑状态（如正在编辑的行、脏数据、撤销栈）**不**持久化到 `TableViewConfig`。
  - 列的编辑配置（`editConfig`）和默认编辑模式（`mode`）可以作为视图配置的一部分持久化。
- **自动保存 vs 手动保存策略**：
  - **单元格编辑模式**：推荐**自动保存**（失去焦点或按 Enter 时触发），结合乐观更新提供流畅体验。
  - **行编辑/批量编辑模式**：推荐**手动保存**，在工具栏提供明确的“保存”和“取消”按钮，避免频繁请求，同时在离开页面或切换视图时，若存在脏数据，需弹出拦截提示。

### 六、低代码（AMIS）集成方案

- **AMIS Schema 支持**：
  - 在 AMIS 的 CRUD 组件中，配置列的 `quickEdit` 属性，支持 `mode: 'inline'` 或弹出表单。
  - 对于批量编辑，开启 CRUD 的 `quickSaveApi` 和 `quickSaveItemApi`。
- **自定义插件注册**：
  - 在 `business-plugins.ts` 中，扩展 `atlas-dynamic-table` 插件，增加编辑相关的配置项（如开启行编辑、批量编辑的开关）。
  - 编写适配器，将 AMIS 的 `quickSaveApi` 请求转换为后端的 `PUT /dynamic-tables/{key}/records` 批量更新接口，并处理等保要求的 `Idempotency-Key` 和 `X-CSRF-TOKEN`。

### 七、优先级与实施建议

- **分级实施**：
  - **基础版（P0）**：完善单元格编辑（支持更多类型、基础校验），实现行编辑模式，支持手动保存/取消。
  - **进阶版（P1）**：实现批量编辑、脏数据提示、键盘导航（Enter/Tab切换）、新增/删除行。
  - **高阶版（P2）**：实现撤销重做、复制粘贴（Excel互通）、复杂联动校验、乐观更新回滚。

- **预估工作量**：
  - 基础版：3 人天
  - 进阶版：4 人天
  - 高阶版：5 人天
  - **总计：12 人天**

- **关键风险与注意事项**：
  - **性能问题**：批量编辑模式下，大量单元格同时处于编辑态可能导致 Vue 渲染卡顿，需结合虚拟滚动（维度2）进行优化。
  - **数据一致性**：多用户并发编辑同一记录时可能产生冲突，后端需提供乐观锁（如基于 `version` 字段）支持，前端需处理冲突报错并提示用户刷新。
  - **安全合规**：编辑操作必须严格校验字段权限（`DynamicFieldPermissionRule.canEdit`），并在请求头中携带 CSRF Token 和幂等键。

---

## 维度 6：性能能力

- **现状评估**：缺失
- **实施优先级**：P1
- **预估人天**：15 天

### 一、现状分析

根据提供的代码库摘要，SecurityPlatform 项目在表格性能能力方面存在明显的缺失。Ant Design Vue 4.x 本身提供了基础的表格组件 `a-table`，但其默认实现对于大数据量场景的性能优化能力有限，需要额外的封装和配置。

- **代码库中已有哪些相关实现（文件路径 + 功能说明）**
  - `composables/useTableView.ts`: 提供了列配置管理、视图持久化、密度控制等功能，但未涉及表格渲染性能优化。
  - `composables/useCrudPage.ts`: 集成了 `useTableView`，并支持分页、搜索等基础 CRUD 功能。分页功能是大数据量处理的一种方式，但并非虚拟滚动等渲染层面的优化。
  - `types/api.ts`: 定义了 `TableViewConfig`，其中包含 `pagination`、`sort`、`filters` 等与数据请求相关的配置，这些配置有助于服务端查询和数据加载优化，但同样不涉及前端渲染性能。
  - `types/dynamic-tables.ts`: 定义了 `DynamicRecordQueryRequest`，包含 `pageIndex/pageSize/keyword/sortBy/sortDesc/filters`，这表明后端支持分页查询和过滤，为前端实现服务端查询提供了基础。

- **存在哪些缺口和不足**
  - **虚拟滚动（行虚拟/列虚拟）**: 摘要中明确指出“虚拟滚动 - 未实现”。这意味着当前表格在渲染大量数据时，所有行和列都会被渲染到 DOM 中，导致严重的性能问题，尤其是当数据量达到数千甚至上万行时。
  - **大数据量渲染优化**: 缺乏针对大数据量场景的整体渲染优化策略，如分块渲染、惰性加载等。
  - **局部刷新**: 未提及针对表格数据局部更新时的优化机制，可能导致不必要的全表重新渲染。
  - **惰性加载/分块渲染**: 未实现，这两种技术对于减少首次加载时间和内存占用至关重要。
  - **缓存**: 未提及前端数据缓存机制，可能导致重复请求相同数据。
  - **防抖搜索**: `useTableView.ts` 中提到了 `scheduleAutoSave` 具有 400ms 防抖，但这是针对视图保存的，而非针对表格数据搜索的防抖。`useCrudPage.ts` 中的搜索功能可能没有内置防抖机制，导致频繁请求。
  - **滚动同步优化**: 对于多表格或复杂布局下的滚动同步，未提及相关实现。

### 二、封装目标

该维度的封装目标是全面提升表格在处理大数据量时的用户体验和系统性能，确保在普通面板和低代码场景下都能提供流畅的交互。

- **该维度需要封装哪些核心能力（对照用户需求清单逐条说明）**
  - **虚拟滚动（行虚拟/列虚拟）**: 实现表格行和列的虚拟化，只渲染可视区域内的 DOM 元素，显著降低 DOM 节点数量，解决大数据量下的卡顿问题。这是性能优化的核心。
  - **大数据量渲染优化**: 提供统一的策略来处理大数据量，包括虚拟滚动、分块渲染和惰性加载。
  - **局部刷新**: 优化数据更新机制，确保只有发生变化的行或单元格进行重新渲染，避免不必要的全表更新。
  - **惰性加载/分块渲染**: 支持在滚动时按需加载数据或分批渲染数据，减少初始渲染时间。
  - **服务端查询**: 结合 `useCrudPage.ts` 和 `types/api.ts` 中已有的分页、排序、过滤等能力，封装统一的服务端查询接口，支持按需从后端获取数据。
  - **缓存**: 实现前端数据缓存策略，减少重复数据请求，提升响应速度。
  - **防抖搜索**: 为表格的搜索、过滤等操作提供统一的防抖机制，避免短时间内频繁触发数据请求。
  - **滚动同步优化**: 提供 API 或配置，支持多个表格或特定区域的滚动位置同步。

- **普通面板场景 vs 低代码场景的差异处理**
  - **普通面板场景**: 封装后的组件或 Composable 应提供丰富的 Props 和配置项，允许开发者灵活控制各项性能优化策略，例如虚拟滚动的阈值、分块加载的大小、缓存策略等。开发者可以直接在 Vue 组件中使用这些能力。
  - **低代码场景（AMIS）**: 需要将这些性能优化能力通过 AMIS Schema 进行抽象和暴露。这意味着需要定义新的 Schema 属性或扩展现有属性，以便在 AMIS 编辑器中配置虚拟滚动、服务端查询等。可能需要注册自定义 AMIS 插件来桥接 Vue 组件的复杂逻辑到 AMIS 运行时。

### 三、核心数据模型 / 类型定义

为了支持表格的性能优化，需要在现有 `TableViewConfig` 的基础上进行扩展，增加与性能相关的配置项。

- **给出 TypeScript 接口/类型定义（代码块）**

```typescript
// types/api.ts 或 types/table-performance.ts

/**
 * 表格性能优化配置
 */
export interface TablePerformanceConfig {
  virtualScroll?: {
    enabled: boolean; // 是否启用虚拟滚动
    rowHeight?: number; // 行高，用于计算虚拟滚动区域，必填
    bufferSize?: number; // 虚拟滚动缓冲区大小，默认 50
    threshold?: number; // 启用虚拟滚动的行数阈值，默认 100
    colVirtualEnabled?: boolean; // 是否启用列虚拟滚动
    colWidths?: Record<string, number>; // 列宽，用于列虚拟滚动
  };
  lazyLoad?: {
    enabled: boolean; // 是否启用惰性加载
    loadThreshold?: number; // 距离底部多少像素开始加载
  };
  chunkRender?: {
    enabled: boolean; // 是否启用分块渲染
    chunkSize?: number; // 每次渲染的行数
    delay?: number; // 每次渲染的延迟（ms）
  };
  debounceSearch?: {
    enabled: boolean; // 是否启用搜索防抖
    delay?: number; // 防抖延迟（ms），默认 300
  };
  dataCache?: {
    enabled: boolean; // 是否启用数据缓存
    strategy?: 'lru' | 'fifo'; // 缓存策略
    maxSize?: number; // 最大缓存条目数
  };
  scrollSyncGroup?: string; // 滚动同步组ID，用于多表格滚动同步
}

/**
 * 扩展 TableViewConfig 以包含性能配置
 */
export interface TableViewConfig {
  // ... 现有配置
  performance?: TablePerformanceConfig; // 新增性能配置
}

/**
 * 服务端查询参数扩展
 */
export interface DynamicRecordQueryRequest {
  pageIndex: number;
  pageSize: number;
  keyword?: string;
  sortBy?: string;
  sortDesc?: boolean;
  filters?: any[];
  // ... 其他查询参数
  // 新增用于区分是否需要全量数据或特定优化模式的参数
  performanceMode?: 'virtual' | 'lazy' | 'chunk';
}
```

- **说明与现有 TableViewConfig 的集成方式**
  通过在 `TableViewConfig` 接口中新增一个可选的 `performance?: TablePerformanceConfig;` 属性，可以将所有与表格性能相关的配置集中管理。这样，每个保存的表格视图都可以拥有独立的性能优化配置。当加载一个视图时，`useTableView` 将会读取 `TableViewConfig.performance` 并将其传递给表格组件，从而激活相应的性能优化策略。

### 四、组件/Composable 封装方案

为了实现表格的性能能力，建议在现有 `composables` 和 `components` 目录下进行扩展和新增。

- **新增或改造哪些文件（给出文件路径建议）**
  - `composables/useTablePerformance.ts` (新增): 核心 Composable，负责封装虚拟滚动、惰性加载、分块渲染、防抖搜索等逻辑。
  - `components/table/ProTable.vue` (新增/改造): 一个高级表格组件，内部集成 `a-table` 并应用 `useTablePerformance` 提供的能力。
  - `components/table/virtual-scroll-container.vue` (新增): 一个通用的虚拟滚动容器组件，可用于行虚拟化和列虚拟化。
  - `composables/useCrudPage.ts` (改造): 引入 `useTablePerformance`，并根据 `TableViewConfig.performance` 调整数据加载和渲染逻辑。
  - `types/table-performance.ts` (新增): 存放 `TablePerformanceConfig` 等性能相关的类型定义。

- **关键 Props/Emits/Expose 设计（代码块）**

  **`components/table/ProTable.vue`**

  ```typescript
  // Props
  interface ProTableProps {
    dataSource: any[]; // 表格数据
    columns: TableViewColumn[]; // 列配置
    performanceConfig?: TablePerformanceConfig; // 性能配置
    loading?: boolean; // 加载状态
    total?: number; // 总数据量，用于分页和虚拟滚动计算
    rowKey?: string | ((record: any) => string); // 行 key
    // ... 其他 Ant Design Vue a-table 的 Props
  }

  // Emits
  interface ProTableEmits {
    (e: 'update:pagination', pagination: any): void; // 分页变化
    (e: 'update:filters', filters: any): void; // 过滤变化
    (e: 'update:sorter', sorter: any): void; // 排序变化
    (e: 'lazy-load', params: { pageIndex: number; pageSize: number }): void; // 惰性加载触发
    (e: 'scroll-sync', scrollTop: number, scrollLeft: number): void; // 滚动同步事件
  }

  // Expose
  interface ProTableExpose {
    scrollTo: (options: { x?: number; y?: number }) => void; // 滚动到指定位置
    refresh: () => void; // 刷新表格数据
  }
  ```

  **`composables/useTablePerformance.ts`**

  ```typescript
  import { ref, computed, watch, onMounted, onUnmounted } from 'vue';
  import type { TablePerformanceConfig } from '@/types/table-performance';

  interface UseTablePerformanceOptions {
    data: Ref<any[]>;
    total: Ref<number>;
    rowHeight: Ref<number>;
    performanceConfig: Ref<TablePerformanceConfig | undefined>;
    onLazyLoad?: (params: { pageIndex: number; pageSize: number }) => void;
    onScrollSync?: (scrollTop: number, scrollLeft: number) => void;
  }

  export function useTablePerformance(options: UseTablePerformanceOptions) {
    const { data, total, rowHeight, performanceConfig, onLazyLoad, onScrollSync } = options;

    const visibleData = ref<any[]>([]);
    const scrollTop = ref(0);
    const scrollLeft = ref(0);

    // 虚拟滚动逻辑
    const enableVirtualScroll = computed(() => performanceConfig.value?.virtualScroll?.enabled && total.value > (performanceConfig.value.virtualScroll.threshold || 100));
    // ... 虚拟滚动计算可见数据、滚动位置等

    // 惰性加载逻辑
    const enableLazyLoad = computed(() => performanceConfig.value?.lazyLoad?.enabled);
    // ... 监听滚动事件，触发 onLazyLoad

    // 防抖搜索逻辑
    const debouncedSearch = (callback: Function) => {
      // ... 实现防抖函数
    };

    // 滚动同步逻辑
    const scrollContainerRef = ref<HTMLElement | null>(null);
    const handleScroll = (e: Event) => {
      const target = e.target as HTMLElement;
      scrollTop.value = target.scrollTop;
      scrollLeft.value = target.scrollLeft;
      if (performanceConfig.value?.scrollSyncGroup && onScrollSync) {
        onScrollSync(scrollTop.value, scrollLeft.value);
      }
    };

    onMounted(() => {
      scrollContainerRef.value?.addEventListener('scroll', handleScroll);
    });

    onUnmounted(() => {
      scrollContainerRef.value?.removeEventListener('scroll', handleScroll);
    });

    return {
      visibleData,
      scrollTop,
      scrollLeft,
      scrollContainerRef,
      debouncedSearch,
      // ... 其他暴露的性能相关状态和方法
    };
  }
  ```

- **核心实现逻辑说明**
  1.  **虚拟滚动**: `useTablePerformance` 内部将维护一个 `visibleData` 响应式变量。通过监听表格容器的滚动事件，结合 `rowHeight` 和 `bufferSize` 计算当前可视区域应渲染的数据索引范围，并更新 `visibleData`。`ProTable.vue` 将 `visibleData` 传递给 `a-table` 进行渲染。列虚拟化类似，需要计算可见列的索引范围。
  2.  **惰性加载/分块渲染**: 同样通过监听滚动事件，当滚动位置接近底部或达到预设阈值时，触发 `onLazyLoad` 事件，通知父组件加载更多数据。分块渲染则是在初始加载时，将大数据量拆分成小块，分批次渲染，每次渲染后通过 `requestAnimationFrame` 或 `setTimeout` 延迟，避免阻塞 UI。
  3.  **服务端查询**: `useCrudPage.ts` 将根据 `TableViewConfig.performance` 中的相关配置（如 `debounceSearch.enabled`），对搜索、过滤、排序等操作进行防抖处理，并调用后端 API 进行数据查询。查询时可以根据 `performanceMode` 参数告知后端当前前端的优化模式，以便后端进行相应的数据准备。
  4.  **缓存**: `useTablePerformance` 可以内置一个简单的 LRU 或 FIFO 缓存，根据查询参数对数据进行缓存。当发起相同查询时，优先从缓存中获取数据。
  5.  **滚动同步**: `useTablePerformance` 暴露 `scrollContainerRef` 和 `handleScroll`。当 `scrollSyncGroup` 存在时，`handleScroll` 会触发 `onScrollSync` 事件，将滚动位置广播出去。其他订阅了相同 `scrollSyncGroup` 的表格组件可以监听此事件并同步自己的滚动位置。
  6.  **局部刷新**: Ant Design Vue 的 `a-table` 在 `dataSource` 变化时会进行局部更新，但对于大数据量，仍然可能导致性能问题。虚拟滚动本身就提供了局部刷新的能力，因为只有可视区域的数据会更新。对于非虚拟滚动的场景，可以通过 `key` 属性的合理使用和 `shouldComponentUpdate` 类似的机制（在 Vue 3 中通过 `v-memo` 或手动优化）来减少不必要的渲染。

### 五、与持久化视图的集成

性能相关的配置需要能够随视图一起保存和加载，以确保用户在不同视图下获得一致的性能体验。

- **该维度的状态如何持久化到 TableViewConfig**
  如第三节所述，通过在 `TableViewConfig` 中新增 `performance?: TablePerformanceConfig;` 属性，可以将所有性能相关的配置（如虚拟滚动是否启用、行高、防抖延迟等）作为视图的一部分进行持久化。当用户通过 `table-view-toolbar.vue` 保存或另存为视图时，`useTableView.ts` 会将当前的 `TablePerformanceConfig` 序列化并存储到后端。

  具体流程：
  1.  用户在 `ProTable.vue` 中调整性能相关设置（例如，启用虚拟滚动，设置行高）。
  2.  `ProTable.vue` 将这些设置通过 `performanceConfig` Prop 传递给 `useTablePerformance`。
  3.  `useTablePerformance` 内部维护这些设置的响应式状态。
  4.  当用户点击“保存视图”时，`table-view-toolbar.vue` 会触发 `useTableView.ts` 的 `saveView` 或 `saveAs` 方法。
  5.  `useTableView.ts` 在构建 `TableViewConfig` 对象时，会从 `ProTable.vue` 获取当前的 `performanceConfig` 状态，并将其赋值给 `TableViewConfig.performance` 属性。
  6.  `api-system.ts` 调用后端 API (`createTableView` 或 `updateTableView`) 将完整的 `TableViewConfig` (包括 `performance` 属性) 存储到数据库。

- **自动保存 vs 手动保存策略**
  - **自动保存**: `useTableView.ts` 中已有的 `scheduleAutoSave` (400ms 防抖) 机制可以扩展到性能配置。当 `performanceConfig` 发生变化时，触发自动保存，将最新的性能配置持久化到当前视图。这可以确保用户在不显式点击保存的情况下，其性能偏好也能被记录。
  - **手动保存**: 用户通过 `table-view-toolbar.vue` 的“保存”或“另存为”功能，显式地保存当前视图的所有配置，包括性能配置。这适用于用户希望明确控制视图状态的场景。

  建议两种策略并存，自动保存提供便利性，手动保存提供最终确认和命名视图的能力。

### 六、低代码（AMIS）集成方案

将表格的性能能力集成到 AMIS 低代码平台，需要通过 AMIS Schema 来描述和配置这些能力。

- **如何在 AMIS Schema 中支持该能力**
  AMIS Schema 可以通过扩展 `table` 组件的属性来支持性能配置。可以在 `columns` 定义中增加 `virtualScroll`、`lazyLoad` 等属性，或者在 `table` 组件的顶层增加一个 `performance` 属性。

  **方案一：扩展 `table` 组件顶层属性**
  在 AMIS `table` 组件的 Schema 中新增一个 `performance` 属性，其结构与 `TablePerformanceConfig` 对应。

  ```json
  {
    

---

## 维度 7：交互体验能力

- **现状评估**：部分
- **实施优先级**：P0
- **预估人天**：18 天

## 当前任务：分析维度 维度7：交互体验能力（单元格tooltip/复制单元格内容/文本选中/点击跳转详情/链接单元格/数字日期金额格式化/高亮匹配搜索词/键盘导航/快捷键/hover工具条/列设置面板/自定义视图/拖拽反馈/加载占位体验）

### 一、现状分析

**代码库中已有哪些相关实现（文件路径 + 功能说明）**

1.  **`composables/useTableView.ts`**: 
    *   **列配置管理**: 提供了列的可见性（visible）、顺序（order）、宽度（width）、固定（pinned）、对齐（align）和省略（ellipsis）等配置。其中 `ellipsis` 属性与单元格 `tooltip` 紧密相关，当内容溢出时通常需要显示 tooltip。
    *   **视图持久化**: 支持视图的保存、另存为、设为默认和重置功能，这为“自定义视图”提供了核心的后端支持和状态管理。
    *   **密度控制**: 提供了表格密度的切换功能（compact/default/comfortable），影响表格的视觉呈现，间接影响交互体验。

2.  **`components/table/editable-cell.vue`**: 
    *   实现了单元格的点击编辑功能，支持文本、数字和选择框类型。这为“点击跳转详情”或“链接单元格”提供了一种交互模式的参考，但当前主要用于数据编辑而非跳转。

3.  **`components/table/table-view-toolbar.vue`**: 
    *   **视图切换下拉**: 允许用户选择和切换不同的表格视图，直接支持“自定义视图”功能。
    *   **保存/另存为/设为默认/重置**: 提供了视图管理的操作入口，与 `useTableView.ts` 配合实现视图的持久化。
    *   **列设置面板**: 提供了列的显示/隐藏、上移/下移、固定左右等操作界面，直接支持“列设置面板”功能。
    *   **密度切换菜单**: 提供了表格密度切换的UI入口。

4.  **`types/api.ts`**: 
    *   **`TableViewColumnConfig`**: 包含 `tooltip` 字段，表明在类型定义层面已经考虑了单元格 tooltip 的配置。同时 `ellipsis` 字段也存在，进一步印证了对溢出内容处理的关注。

**存在哪些缺口和不足**

1.  **单元格tooltip**: 尽管 `TableViewColumnConfig` 中有 `tooltip` 字段，但前端 Ant Design Vue `a-table` 的具体实现细节（如如何根据内容溢出自动显示，或支持自定义渲染）在代码库摘要中未明确体现。
2.  **复制单元格内容**: 未发现相关实现。
3.  **文本选中**: 未发现相关实现，Ant Design Vue `a-table` 默认行为可能支持，但未有明确的封装或控制。
4.  **点击跳转详情/链接单元格**: `editable-cell.vue` 提供了编辑能力，但没有直接的点击跳转或链接渲染功能。
5.  **数字日期金额格式化**: 未发现通用的格式化能力，可能依赖于业务层手动处理。
6.  **高亮匹配搜索词**: 未发现相关实现，对于表格内部搜索结果的视觉反馈缺失。
7.  **键盘导航**: 代码库摘要中明确指出“未实现”，这是一个重要的交互缺口。
8.  **快捷键**: 未发现相关实现。
9.  **hover工具条**: 未发现相关实现，即在单元格或行 hover 时出现操作工具条。
10. **拖拽反馈**: 未发现相关实现，例如列宽调整、行拖拽排序等操作的视觉反馈。
11. **加载占位体验**: 未发现明确的骨架屏或加载占位符的封装，可能依赖于 Ant Design Vue `a-table` 自身的 `loading` 状态。
12. **列设置面板/自定义视图**: 现有功能已具备基础框架，但可能在用户体验、高级配置（如列分组、列冻结的更细粒度控制）等方面存在提升空间。

### 二、封装目标

**该维度需要封装哪些核心能力（对照用户需求清单逐条说明）**

1.  **单元格tooltip**: 
    *   **目标**: 提供灵活的单元格内容溢出 tooltip 机制，支持纯文本 tooltip 和自定义渲染 tooltip（如包含复杂信息或操作）。
    *   **实现**: 默认根据 `ellipsis` 自动判断是否显示 tooltip；允许通过列配置显式开启/关闭 tooltip；支持 `tooltipRender` 函数或插槽来自定义 tooltip 内容。

2.  **复制单元格内容**: 
    *   **目标**: 提供便捷的单元格内容复制功能，支持点击复制图标或通过快捷键复制。
    *   **实现**: 在列配置中增加 `copyable` 属性；在单元格 hover 时显示复制图标；集成剪贴板操作库。

3.  **文本选中**: 
    *   **目标**: 允许用户自由选中单元格内的文本内容。
    *   **实现**: 确保单元格内容可被浏览器默认选中，必要时调整 CSS 样式（如 `user-select` 属性）。

4.  **点击跳转详情/链接单元格**: 
    *   **目标**: 支持单元格内容渲染为链接，或点击单元格触发路由跳转/打开侧边抽屉等操作。
    *   **实现**: 在列配置中增加 `link` 属性（布尔值或函数返回链接地址）；增加 `onCellClick` 事件，允许自定义点击行为；支持 `cellRender` 函数或插槽渲染链接。

5.  **数字日期金额格式化**: 
    *   **目标**: 提供统一、可配置的数字、日期、金额等数据类型的格式化能力。
    *   **实现**: 在列配置中增加 `format` 属性，支持预设格式（如 `currency`, `date`, `datetime`）和自定义格式字符串；集成 `dayjs` 或 `numeral.js` 等库进行格式化。

6.  **高亮匹配搜索词**: 
    *   **目标**: 当表格数据经过搜索过滤后，高亮显示单元格内匹配的搜索词。
    *   **实现**: 封装一个 `HighlightText` 组件；在表格渲染时，将搜索词作为 props 传入，由 `HighlightText` 组件负责匹配和高亮。

7.  **键盘导航**: 
    *   **目标**: 实现表格内部的键盘上下左右导航、Enter/Space 选中/激活、Tab 切换焦点等。
    *   **实现**: 监听键盘事件，管理当前焦点单元格状态；通过 `aria-activedescendant` 或 `tabindex` 属性实现无障碍导航。

8.  **快捷键**: 
    *   **目标**: 支持常用操作的快捷键，如复制单元格、切换视图、导出等。
    *   **实现**: 统一管理表格组件内的快捷键，避免冲突；集成 `vueuse/core` 的 `useMagicKeys` 或类似库。

9.  **hover工具条**: 
    *   **目标**: 在行或单元格 hover 时，显示可配置的操作工具条。
    *   **实现**: 在行或单元格的 `mouseover`/`mouseleave` 事件中控制工具条的显示与隐藏；工具条内容通过插槽或配置项传入。

10. **列设置面板**: 
    *   **目标**: 增强现有列设置面板，支持列分组、列冻结（左右）、列宽拖拽调整的持久化。
    *   **实现**: 改造现有面板 UI，增加分组和冻结配置项；集成列宽调整功能，并将调整后的宽度持久化到视图配置。

11. **自定义视图**: 
    *   **目标**: 完善自定义视图功能，支持视图的导入导出、共享、权限管理。
    *   **实现**: 扩展 `table-view-toolbar.vue`，增加导入导出按钮；与后端 API 配合实现视图共享和权限控制。

12. **拖拽反馈**: 
    *   **目标**: 为列宽调整、行拖拽排序等操作提供清晰的视觉反馈。
    *   **实现**: 利用 Ant Design Vue 的拖拽组件或自定义拖拽指令，在拖拽过程中显示占位符、指示线等。

13. **加载占位体验**: 
    *   **目标**: 提供友好的表格数据加载占位体验，如骨架屏。
    *   **实现**: 封装一个 `TableSkeleton` 组件，在表格 `loading` 状态时显示；支持配置骨架屏的行数、列数等。

**普通面板场景 vs 低代码场景的差异处理**

*   **普通面板场景**: 主要通过 Vue 组件的 Props、Emits 和插槽进行配置和扩展。封装的组件和 Composable 将直接在 Vue 页面中使用，提供丰富的交互能力。
*   **低代码场景（AMIS）**: 
    *   **AMIS Schema 扩展**: 核心交互能力需要通过扩展 AMIS 的 `a-table` 渲染器或自定义组件来实现。例如，单元格的 `tooltip`、`copyable`、`link`、`format` 等属性可以直接映射到 AMIS 的 `column` 配置中。
    *   **自定义组件/插件**: 对于复杂的交互（如键盘导航、hover 工具条、高级列设置面板），可能需要注册自定义的 AMIS 插件或自定义组件，将 Vue 封装的能力桥接到 AMIS 环境中。
    *   **数据模型映射**: 确保 Vue 组件的 Props 和 Emits 能够与 AMIS Schema 的数据结构进行有效映射和转换。

### 三、核心数据模型 / 类型定义

为了支持上述交互体验能力，我们需要扩展现有的 `TableViewColumnConfig` 和 `TableViewConfig` 类型，并可能引入新的类型定义。

**给出 TypeScript 接口/类型定义（代码块）**

```typescript
// types/table-interactive.ts (新增文件)

import type { TableViewColumnConfig } from './api'; // 引入现有类型

/**
 * 单元格格式化类型
 */
export type CellFormatType = 'text' | 'number' | 'currency' | 'date' | 'datetime' | 'percent';

/**
 * 单元格格式化配置
 */
export interface CellFormatOptions {
  type: CellFormatType;
  pattern?: string; // 格式化模式，如 'YYYY-MM-DD', '0,0.00'
  locale?: string; // 国际化语言环境，如 'zh-CN', 'en-US'
  prefix?: string; // 前缀，如货币符号
  suffix?: string; // 后缀，如百分号
}

/**
 * 单元格链接配置
 */
export interface CellLinkOptions {
  to?: string | ((record: any) => string); // 路由路径或生成路径的函数
  target?: '_blank' | '_self'; // 链接打开方式
  onClick?: (record: any) => void; // 点击事件回调
}

/**
 * 扩展 TableViewColumnConfig，增加交互体验相关配置
 */
export interface InteractiveTableViewColumnConfig extends TableViewColumnConfig {
  copyable?: boolean; // 是否可复制单元格内容
  selectable?: boolean; // 是否可选中单元格文本
  format?: CellFormatOptions; // 单元格格式化配置
  link?: CellLinkOptions; // 链接单元格配置
  highlightSearch?: boolean; // 是否高亮匹配搜索词
  tooltipRender?: (text: any, record: any, index: number) => string | VNode; // 自定义 tooltip 渲染函数或 VNode
  hoverActions?: Array<{ // hover 工具条动作配置
    key: string;
    icon: string; // 图标名称
    label: string; // 动作名称
    onClick: (record: any) => void;
    auth?: string; // 权限码
  }>;
}

/**
 * 表格交互体验配置
 */
export interface TableInteractiveConfig {
  enableKeyboardNavigation?: boolean; // 是否启用键盘导航
  enableHotkeys?: boolean; // 是否启用快捷键
  enableColumnResizeFeedback?: boolean; // 是否启用列宽调整拖拽反馈
  enableRowDragFeedback?: boolean; // 是否启用行拖拽排序反馈
  loadingSkeleton?: {
    enable?: boolean; // 是否启用加载骨架屏
    rows?: number; // 骨架屏行数
    columns?: number; // 骨架屏列数
  }; 
}

// types/api.ts (修改现有文件)

// 扩展 TableViewConfig 以包含交互体验配置
export interface TableViewConfig {
  // ... 现有属性
  columns: InteractiveTableViewColumnConfig[]; // 使用扩展后的列配置
  interactive?: TableInteractiveConfig; // 新增交互体验配置
}
```

**说明与现有 TableViewConfig 的集成方式**

*   **`InteractiveTableViewColumnConfig`**: 通过接口继承 (`extends TableViewColumnConfig`) 的方式，在 `types/table-interactive.ts` 中定义新的列配置属性，如 `copyable`, `format`, `link`, `highlightSearch`, `tooltipRender`, `hoverActions` 等。然后，在 `types/api.ts` 中，将 `TableViewConfig` 的 `columns` 属性的类型从 `TableViewColumnConfig[]` 修改为 `InteractiveTableViewColumnConfig[]`，从而实现对列配置的扩展。
*   **`TableInteractiveConfig`**: 新增一个独立的接口 `TableInteractiveConfig` 来管理表格层面的全局交互体验配置，如 `enableKeyboardNavigation`, `enableHotkeys`, `loadingSkeleton` 等。然后，在 `types/api.ts` 的 `TableViewConfig` 接口中新增一个可选属性 `interactive?: TableInteractiveConfig`，用于存储这些全局配置。这样，所有交互体验相关的配置都可以通过 `TableViewConfig` 进行统一管理和持久化。

### 四、组件/Composable 封装方案

**新增或改造哪些文件（给出文件路径建议）**

1.  **`composables/useTableInteractive.ts` (新增)**: 核心 Composable，负责管理表格的键盘导航、快捷键、加载状态等全局交互逻辑。
2.  **`components/table/InteractiveTable.vue` (新增)**: 封装 Ant Design Vue `a-table`，集成 `useTableInteractive` 和自定义单元格渲染逻辑。
3.  **`components/table/cells/InteractiveCell.vue` (新增)**: 负责单个单元格的交互渲染，如 tooltip、复制、链接、格式化、高亮等。
4.  **`components/table/TableSkeleton.vue` (新增)**: 表格骨架屏组件。
5.  **`components/table/table-view-toolbar.vue` (改造)**: 增加视图导入导出、共享等高级功能入口。
6.  **`types/table-interactive.ts` (新增)**: 上述核心数据模型/类型定义。

**关键 Props/Emits/Expose 设计（代码块）**

```typescript
// components/table/InteractiveTable.vue (部分设计)

<script setup lang="ts">
import { computed, ref } from 'vue';
import { Table as ATable } from 'ant-design-vue';
import type { TableProps } from 'ant-design-vue';
import type { InteractiveTableViewColumnConfig, TableInteractiveConfig } from '@/types/table-interactive';
import InteractiveCell from './cells/InteractiveCell.vue';
import TableSkeleton from './TableSkeleton.vue';
import { useTableInteractive } from '@/composables/useTableInteractive';

interface Props extends TableProps {
  columns: InteractiveTableViewColumnConfig[];
  interactiveConfig?: TableInteractiveConfig;
  loading?: boolean;
  // ... 其他 Ant Design Vue TableProps
}

const props = defineProps<Props>();
const emit = defineEmits(['cellClick', 'rowClick', 'update:columns']);

const { currentFocusedCell, handleKeydown } = useTableInteractive(props.interactiveConfig);

const processedColumns = computed(() => {
  return props.columns.map(col => ({
    ...col,
    customRender: ({ text, record, index, column }) => {
      // 统一处理单元格渲染，注入 InteractiveCell
      return h(InteractiveCell, {
        text,
        record,
        index,
        column: column as InteractiveTableViewColumnConfig,
        onCellClick: (data) => emit('cellClick', data),
        // ... 传递其他 InteractiveCell props
      });
    },
  }));
});

// Expose 键盘导航相关方法，供外部调用
defineExpose({
  focusCell: (rowIndex: number, colKey: string) => { /* ... */ },
  // ...
});
</script>

<template>
  <div @keydown="handleKeydown">
    <TableSkeleton v-if="loading && interactiveConfig?.loadingSkeleton?.enable" :rows="interactiveConfig.loadingSkeleton.rows" :columns="interactiveConfig.loadingSkeleton.columns" />
    <a-table
      v-else
      v-bind="$attrs"
      :columns="processedColumns"
      :data-source="dataSource"
      :loading="loading"
      @rowClick="(record, index, event) => emit('rowClick', { record, index, event })"
    >
      <template #bodyCell="scope">
        <slot name="bodyCell" v-bind="scope"></slot>
      </template>
      <!-- 其他插槽透传 -->
    </a-table>
  </div>
</template>


// components/table/cells/InteractiveCell.vue (部分设计)

<script setup lang="ts">
import { computed, h, VNode } from 'vue';
import { Tooltip as ATooltip, TypographyParagraph as AParagraph } from 'ant-design-vue';
import { CopyOutlined, LinkOutlined } from '@ant-design/icons-vue';
import type { InteractiveTableViewColumnConfig, CellFormatOptions, CellLinkOptions } from '@/types/table-interactive';
import { formatValue } from '@/utils/formatters'; // 假设存在格式化工具函数

interface Props {
  text: any;
  record: any;
  index: number;
  column: InteractiveTableViewColumnConfig;
}

const props = defineProps<Props>();
const emit = defineEmits(["cellClick"]);

const formattedText = computed(() => {
  if (props.column.format) {
    return formatValue(props.text, props.column.format);
  }
  return props.text;
});

const cellContent = computed(() => {
  let content: any = formattedText.value;

  // 高亮搜索词
  if (props.column.highlightSearch) {
    // 假设搜索词通过 provide/inject 或全局状态管理获取
    const searchTerm = ""; // TODO: 从 provide/inject 获取搜索词
    if (searchTerm && typeof content === 'string') {
      const regex = new RegExp(`(${searchTerm})`, 'gi');
      content = content.replace(regex, '<span style="background-color: yellow;">$1</span>');
      return h('span', { innerHTML: content });
    }
  }

  // 链接单元格
  if (props.column.link) {
    const linkOptions: CellLinkOptions = props.column.link;
    const href = typeof linkOptions.to === 'function' ? linkOptions.to(props.record) : linkOptions.to;
    return h('a', {
      href,
      target: linkOptions.target || '_self',
      onClick: (e: Event) => {
        if (linkOptions.onClick) {
          e.preventDefault();
          linkOptions.onClick(props.record);
        }
        emit('cellClick', { record: props.record, column: props.column, value: props.text, event: e });
      },
    }, [h(LinkOutlined), ' ', content]);
  }

  // 文本选中
  if (props.column.selectable) {
    return h(AParagraph, { copyable: false, ellipsis: false, style: { userSelect: 'text' } }, () => content);
  }

  return content;
});

const tooltipContent = computed(() => {
  if (props.column.tooltipRender) {
    return props.column.tooltipRender(props.text, props.record, props.index);
  }
  // 默认行为：当内容溢出时显示完整文本
  return typeof props.text === 'string' ? props.text : undefined;
});

const handleCopy = () => {
  if (props.column.copyable && typeof formattedText.value === 'string') {
    navigator.clipboard.writeText(formattedText.value);
    // TODO: 显示复制成功提示
  }
};

const handleCellClick = (e: Event) => {
  if (!props.column.link) { // 如果不是链接单元格，才触发普通点击事件
    emit('cellClick', { record: props.record, column: props.column, value: props.text, event: e });
  }
};
</script>

<template>
  <div class="interactive-cell" @click="handleCellClick">
    <a-tooltip v-if="column.ellipsis || column.tooltipRender" :title="tooltipContent">
      <span class="cell-content">{{ cellContent }}</span>
    </a-tooltip>
    <span v-else class="cell-content">{{ cellContent }}</span>

    <CopyOutlined v-if="column.copyable" class="copy-icon" @click.stop="handleCopy" />
    <!-- hover 工具条插槽或渲染 -->
  </div>
</template>

<style scoped>
.interactive-cell {
  display: flex;
  align-items: center;
  gap: 4px;
}
.cell-content {
  flex-grow: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap; /* 默认不换行，配合 ellipsis */
}
.copy-icon {
  cursor: pointer;
  color: #1890ff;
}
</style>


// composables/useTableInteractive.ts (部分设计)

import { ref, onMounted, onUnmounted, watch } from 'vue';
import type { TableInteractiveConfig } from '@/types/table-interactive';
import { useMagicKeys } from '@vueuse/core'; // 假设已安装 @vueuse/core

export function useTableInteractive(config?: TableInteractiveConfig) {
  const currentFocusedCell = ref<{ rowIndex: number; colKey: string } | null>(null);

  // 键盘导航
  const handleKeydown = (event: KeyboardEvent) => {
    if (!config?.enableKeyboardNavigation) return;
    // 实现上下左右、Enter、Tab 等键盘导航逻辑
    // 更新 currentFocusedCell
    console.log('Keyboard event:', event.key);
  };

  // 快捷键
  const { copy, enter } = useMagicKeys(); // 示例：监听 Ctrl+C / Cmd+C 和 Enter

  watch(copy, (v) => {
    if (v) {
      console.log('Ctrl+C / Cmd+C pressed');
      // 实现复制当前焦点单元格内容逻辑
    }
  });

  onMounted(() => {
    if (config?.enableHotkeys) {
      // 注册全局快捷键监听
    }
  });

  onUnmounted(() => {
    // 清理快捷键监听
  });

  return {
    currentFocusedCell,
    handleKeydown,
  };
}


// utils/formatters.ts (新增文件，示例)

import dayjs from 'dayjs';
import numeral from 'numeral';
import type { CellFormatOptions } from '@/types/table-interactive';

export function formatValue(value: any, options: CellFormatOptions): string {
  if (value === null || value === undefined) return '';

  switch (options.type) {
    case 'number':
      return numeral(value).format(options.pattern || '0,0');
    case 'currency':
      return (options.prefix || '') + numeral(value).format(options.pattern || '0,0.00') + (options.suffix || '');
    case 'percent':
      return numeral(value).format(options.pattern || '0.00%');
    case 'date':
      return dayjs(value).locale(options.locale || 'zh-cn').format(options.pattern || 'YYYY-MM-DD');
    case 'datetime':
      return dayjs(value).locale(options.locale || 'zh-cn').format(options.pattern || 'YYYY-MM-DD HH:mm:ss');
    case 'text':
    default:
      return String(value);
  }
}
```

**核心实现逻辑说明**

1.  **`InteractiveTable.vue`**: 
    *   作为 Ant Design Vue `a-table` 的封装层，接收 `columns` (扩展后的 `InteractiveTableViewColumnConfig`) 和 `interactiveConfig` (全局交互配置)。
    *   通过 `processedColumns` 计算属性，遍历 `columns` 并为每个列的 `customRender` 注入 `InteractiveCell` 组件，将单元格内容渲染的职责下放。
    *   集成 `useTableInteractive` Composable，处理表格层面的键盘事件和快捷键。
    *   在 `loading` 状态下，根据 `interactiveConfig.loadingSkeleton.enable` 决定是否显示 `TableSkeleton` 骨架屏。
    *   通过 `defineExpose` 暴露一些方法，如 `focusCell`，以便外部组件可以控制表格的焦点状态。

2.  **`InteractiveCell.vue`**: 
    *   接收 `text` (单元格原始值)、`record` (当前行数据)、`column` (当前列配置) 等 props。
    *   **格式化**: `formattedText` 计算属性根据 `column.format` 调用 `utils/formatters.ts` 中的 `formatValue` 函数进行统一格式化。
    *   **高亮搜索词**: `cellContent` 计算属性中，如果 `column.highlightSearch` 为 true，则从某个地方（例如通过 `provide/inject` 注入的全局搜索状态）获取搜索词，并使用正则表达式对 `formattedText` 进行匹配和高亮。
    *   **链接单元格**: 如果 `column.link` 存在，则渲染 `<a>` 标签，并根据 `link.to` 生成链接地址，同时处理 `onClick` 事件。
    *   **文本选中**: 如果 `column.selectable` 为 true，则渲染 `a-typography-paragraph` 并设置 `user-select: text` 样式。
    *   **复制单元格**: 如果 `column.copyable` 为 true，则在单元格内容旁边显示 `CopyOutlined` 图标，点击时调用 `navigator.clipboard.writeText` 复制 `formattedText`。
    *   **单元格tooltip**: 根据 `column.ellipsis` 或 `column.tooltipRender` 决定是否使用 `a-tooltip`。如果 `tooltipRender` 存在，则使用其返回值作为 tooltip 内容，否则默认显示完整文本。
    *   **hover工具条**: 预留插槽或通过 `column.hoverActions` 配置渲染。

3.  **`useTableInteractive.ts`**: 
    *   管理 `currentFocusedCell` 状态，用于追踪当前键盘导航的焦点单元格。
    *   `handleKeydown` 方法负责监听表格区域的键盘事件，根据按键（上下左右、Enter、Tab等）更新 `currentFocusedCell`，并触发相应的行为（如单元格编辑、行选中等）。
    *   集成 `@vueuse/core` 的 `useMagicKeys` 来监听全局快捷键，例如 `Ctrl+C` 触发复制当前焦点单元格内容。

4.  **`utils/formatters.ts`**: 
    *   提供 `formatValue` 工具函数，集中处理数字、日期、金额等常见数据类型的格式化逻辑，减少业务代码中的重复。

### 五、与持久化视图的集成

**该维度的状态如何持久化到 TableViewConfig**

*   **列级别交互配置**: `InteractiveTableViewColumnConfig` 中新增的 `copyable`, `selectable`, `format`, `link`, `highlightSearch`, `tooltipRender`, `hoverActions` 等属性，将直接作为 `TableViewColumnConfig` 的一部分，存储在 `TableViewConfig.columns` 数组中。当用户通过列设置面板或表格交互操作修改这些配置时，`useTableView` Composable 会捕获这些变化，并将其更新到 `TableViewConfig` 对象中。
*   **表格级别交互配置**: `TableInteractiveConfig` 中新增的 `enableKeyboardNavigation`, `enableHotkeys`, `loadingSkeleton` 等属性，将存储在 `TableViewConfig` 的新属性 `interactive` 中。这些配置通常通过表格组件的 Props 传入，并在 `useTableView` 中进行管理和持久化。
*   **持久化流程**: 当用户保存视图（`saveView` / `saveAs`）或触发自动保存（`scheduleAutoSave`）时，完整的 `TableViewConfig` 对象（包括扩展的交互体验配置）会被序列化并发送到后端 API (`updateTableViewConfig`) 进行存储。后端会根据 `tableKey` 和 `userId` 存储用户的个性化视图配置。

**自动保存 vs 手动保存策略**

*   **自动保存**: 对于一些轻量级的、频繁变动的交互状态，如列宽调整、列顺序调整（已存在）、键盘导航的焦点位置（如果需要持久化），可以考虑集成到 `useTableView` 的 `scheduleAutoSave` 机制中。当用户进行这些操作后，经过防抖处理，自动将最新的 `TableViewConfig` (包含交互配置) 更新到后端。这提供了无感知的用户体验。
*   **手动保存**: 对于一些重要的、影响较大的配置，如自定义视图的整体布局、列的显示/隐藏、格式化规则等，仍然需要用户通过点击“保存”、“另存为”按钮进行显式保存。这确保了用户对重要配置的控制权，并避免了意外的自动保存导致配置丢失或混乱。
*   **混合策略**: 建议采用混合策略。默认情况下，大部分交互配置（如格式化、链接、复制等）随视图一起保存。对于一些用户自定义的视图，其交互配置也应随之保存。而像键盘导航的当前焦点等临时状态，则无需持久化，或者仅在客户端进行临时存储。

### 六、低代码（AMIS）集成方案

**如何在 AMIS Schema 中支持该能力**

AMIS 的 `Table` 组件提供了丰富的配置项，但对于 Vue 组件中封装的特定交互能力，需要通过扩展 AMIS Schema 或自定义渲染器来实现。

1.  **扩展 `column` 配置**: 
    *   对于 `copyable`, `selectable`, `format`, `link`, `highlightSearch`, `tooltipRender` 等列级别的交互配置，可以在 AMIS 的 `column` 配置中增加对应的属性。例如：

    ```json
    {
      "type": "table",
      "columns": [
        {
          "name": "name",
          "label": "名称",
          "copyable": true, // 对应 copyable
          "link": {
            "to": "/detail/${id}", // 对应 link.to
            "target": "_blank"
          },
          "format": {
            "type": "text",
            "pattern": ""
          },
          "highlightSearch": true
        },
        {
          "name": "amount",
          "label": "金额",
          "type": "number",
          "format": {
            "type": "currency",
            "pattern": "0,0.00",
            "prefix": "$"
          }
        },
        {
          "name": "createTime",
          "label": "创建时间",
          "type": "datetime",
          "format": {
            "type": "datetime",
            "pattern": "YYYY-MM-DD HH:mm"
          }
        }
      ]
    }
    ```

    *   在 `AmisRenderer.vue` 或 `business-plugins.ts` 中，需要编写逻辑将这些 AMIS Schema 属性映射到 Vue `InteractiveTable` 组件的 Props 上，或者在自定义的 `bodyCell` 渲染器中处理这些属性。

2.  **自定义 `bodyCell` 渲染器**: 
    *   对于更复杂的单元格交互（如 `hoverActions` 或自定义 `tooltipRender`），可以注册一个自定义的 `bodyCell` 渲染器。这个渲染器可以是一个 Vue 组件，它接收 AMIS 传递的 `value`, `data`, `column` 等属性，然后内部使用 `InteractiveCell.vue` 来渲染，并根据 `column` 配置处理交互逻辑。

3.  **表格级别交互配置**: 
    *   对于 `enableKeyboardNavigation`, `enableHotkeys`, `loadingSkeleton` 等表格级别的配置，可以在 AMIS `table` 组件的顶层配置中增加对应的属性，例如 `interactiveConfig`。

    ```json
    {
      "type": "table",
      "interactiveConfig": {
        "enableKeyboardNavigation": true,
        "loadingSkeleton": {
          "enable": true,
          "rows": 5
        }
      },
      "columns": [...]
    }
    ```

    *   同样，需要在 `AmisRenderer.vue` 中将 `interactiveConfig` 属性传递给底层的 Vue `InteractiveTable` 组件。

**是否需要注册自定义 AMIS 插件**

*   **是，强烈建议注册自定义 AMIS 插件。**
*   **原因**: 
    *   **封装复杂逻辑**: 键盘导航、快捷键、高级列设置面板等复杂的交互逻辑，难以直接通过简单的 Schema 属性映射实现。通过注册自定义插件，可以封装这些逻辑，并在 AMIS 环境中以组件或 Composable 的形式复用。
    *   **统一体验**: 确保在普通 Vue 页面和 AMIS 低代码页面中，表格的交互体验保持一致。
    *   **扩展性**: 插件机制使得未来可以更容易地扩展新的交互能力，而无需修改核心的 `InteractiveTable` 组件。
    *   **类型安全**: 可以在插件中定义 AMIS Schema 的类型，提高开发效率和代码质量。
*   **插件内容**: 
    *   注册一个自定义的 `renderer`，用于渲染 `InteractiveTable.vue` 组件，并负责将 AMIS Schema 属性转换为 Vue 组件的 Props。
    *   注册自定义的 `cellRenderer`，用于渲染 `InteractiveCell.vue`，处理单元格级别的交互。
    *   可能需要注册自定义的 `action` 或 `schema` 扩展，以便在 AMIS 编辑器中配置这些交互属性。

### 七、优先级与实施建议

**基础版/进阶版/高阶版分级**

| 功能点                  | 基础版 (P0)                               | 进阶版 (P1)                                       | 高阶版 (P2)                                       |
| :---------------------- | :---------------------------------------- | :------------------------------------------------ | :------------------------------------------------ |
| 单元格tooltip           | 文本溢出自动显示完整文本                  | 支持 `tooltipRender` 自定义渲染                   |                                                   |
| 复制单元格内容          | 点击图标复制                              | 快捷键复制 (Ctrl+C / Cmd+C)                       |                                                   |
| 文本选中                | 默认可选中                                |                                                   |
| 点击跳转详情/链接单元格 | 支持配置 `link.to` 和 `onClick`           | 支持 `cellRender` 自定义渲染                      |                                                   |
| 数字日期金额格式化      | 支持 `format` 属性，提供常见格式化类型    | 支持自定义格式字符串和国际化                      |                                                   |
| 高亮匹配搜索词          | 简单文本高亮                              | 支持多关键词高亮、高亮样式自定义                  |                                                   |
| 键盘导航                | 上下左右导航、Enter 激活                  | Tab 切换焦点、Shift+Tab 反向切换、Home/End 导航 | 支持自定义快捷键映射、无障碍阅读器集成            |
| 快捷键                  | 复制 (Ctrl+C)、保存视图 (Ctrl+S)          | 更多常用操作快捷键 (如导出、刷新)                 | 自定义快捷键配置界面                              |
| hover工具条             | 行 hover 显示简单操作按钮                 | 单元格 hover 显示操作按钮，支持配置化             | 支持自定义渲染、权限控制                          |
| 列设置面板              | 增强现有面板，支持列分组、列冻结          | 列宽拖拽调整的持久化                              | 视图导入导出、共享、权限管理                      |
| 自定义视图              | 完善视图切换、保存、设为默认、重置        | 视图导入导出、共享、权限管理                      |                                                   |
| 拖拽反馈                | 列宽调整、行拖拽排序的视觉反馈            |                                                   |                                                   |
| 加载占位体验            | 骨架屏占位                                |                                                   |                                                   |

**预估工作量（人天）**

*   **P0 基础版**: 5 人天
    *   `InteractiveCell.vue` 基础功能实现 (tooltip, copyable, link, format, highlightSearch)
    *   `InteractiveTable.vue` 集成 `InteractiveCell` 和 `TableSkeleton`
    *   `useTableInteractive.ts` 基础键盘导航和快捷键 (复制)
    *   `types/table-interactive.ts` 类型定义
    *   `utils/formatters.ts` 格式化工具函数
    *   `TableViewConfig` 扩展及持久化集成
*   **P1 进阶版**: 8 人天
    *   `InteractiveCell.vue` 增强 (自定义 tooltipRender, cellRender, hoverActions)
    *   `useTableInteractive.ts` 增强 (更多键盘导航和快捷键)
    *   `table-view-toolbar.vue` 改造 (列设置面板增强，视图导入导出)
    *   AMIS 插件注册 (基础列属性映射，自定义 `bodyCell` 渲染器)
*   **P2 高阶版**: 5 人天
    *   AMIS 插件增强 (复杂交互逻辑桥接，AMIS 编辑器配置支持)
    *   视图共享、权限管理 (与后端 API 联调)
    *   无障碍阅读器集成、自定义快捷键配置界面

**总预估工作量**: 18 人天

**关键风险与注意事项**

1.  **性能问题**: 复杂的单元格渲染逻辑（如高亮、tooltip、hover 工具条）可能对表格的渲染性能造成影响，尤其是在数据量大、列数多的情况下。需要进行性能测试和优化，例如使用虚拟滚动（虽然目前未实现，但未来需要考虑兼容）。
2.  **兼容性**: 确保封装的交互能力与 Ant Design Vue `a-table` 的现有功能和未来升级保持良好兼容性。
3.  **AMIS 集成复杂度**: AMIS 的扩展机制相对灵活，但将 Vue 组件的复杂交互逻辑无缝桥接到 AMIS 环境中可能需要深入理解 AMIS 的渲染原理和插件机制，存在一定的学习成本和集成难度。
4.  **状态管理**: 键盘导航的焦点状态、hover 状态等需要进行精细化的状态管理，避免状态混乱导致交互异常。
5.  **国际化**: 格式化、tooltip 等涉及文本显示的功能需要考虑国际化支持。
6.  **无障碍性**: 键盘导航和快捷键的实现需要严格遵循无障碍设计规范，确保所有用户都能顺畅使用。
7.  **测试**: 交互体验的测试需要覆盖各种场景，包括键盘操作、鼠标操作、不同配置组合等，确保功能的稳定性和用户体验。
8.  **与现有 `editable-cell.vue` 的协调**: 需要考虑 `InteractiveCell.vue` 与 `editable-cell.vue` 在功能上的重叠和协调，例如一个单元格是否可以同时支持编辑和链接。可能需要将 `editable-cell.vue` 的功能集成到 `InteractiveCell.vue` 中，或者设计清晰的优先级和互斥规则。


---

## 维度 8：权限与配置能力

- **现状评估**：部分
- **实施优先级**：P0
- **预估人天**：35 天

### 一、现状分析

当前代码库在权限与配置能力方面已有一些基础实现，但针对表格维度的精细化控制仍存在显著缺口。

- **代码库中已有的相关实现：**
  - `composables/useCrudPage.ts`：提供了基础的 CRUD 权限控制（`canCreate`/`canUpdate`/`canDelete`），这主要针对行级别的操作权限，而非列级别的可见性或操作按钮的细粒度控制。
  - `types/dynamic-tables.ts`：定义了 `DynamicFieldPermissionRule` 类型（`fieldName`/`roleCode`/`canView`/`canEdit`），这表明后端已具备字段级别的权限规则定义能力，为前端实现列权限控制提供了数据模型支持。
  - `composables/useTableView.ts`：负责列配置管理，包括 `visible`/`order`/`width`/`pinned`/`align`/`ellipsis` 等，但这些配置目前是用户可自定义并持久化的，并未与权限系统集成，即无法实现“按角色控制列可见性”。
  - `components/table/table-view-toolbar.vue`：提供了列设置面板，允许用户手动显示/隐藏列，但同样缺乏与权限的联动。
  - `services/api-system.ts`：提供了完整的 `TableView` CRUD API，支持视图的创建、更新、查询、设为默认、复制和删除，这为“不同用户保存个人视图”、“共享视图”、“默认视图/团队视图”等功能提供了后端持久化能力。
  - 后端支持情况中明确指出：`字段权限：GET /dynamic-tables/{key}/field-permissions`，说明后端已具备查询字段权限的接口。

- **存在哪些缺口和不足：**
  - **列可见性控制**：虽然 `useTableView.ts` 可以控制列的 `visible` 属性，但前端渲染层尚未集成后端提供的字段权限接口，无法实现“按角色控制列可见性”。
  - **操作按钮权限控制**：`useCrudPage.ts` 提供的权限控制粒度较粗，未能细化到表格行内特定操作按钮（如“编辑”、“删除”、“查看详情”等）的可见性或可用性控制。
  - **敏感字段脱敏**：目前代码库中未发现针对敏感字段进行脱敏处理的通用机制。
  - **条件显示列**：缺乏根据数据内容或特定业务条件动态显示/隐藏列的能力。
  - **视图共享与团队视图**：虽然视图可以持久化，但缺乏将个人视图共享给其他用户或团队，以及设置团队默认视图的机制。
  - **操作审计**：代码库摘要中未提及针对表格视图配置或数据操作的审计功能。

### 二、封装目标

本维度的封装目标是构建一套全面、灵活且与权限系统深度集成的表格配置与视图管理能力，以满足不同用户在不同场景下的个性化和安全性需求。

- **该维度需要封装的核心能力：**
  - **按角色控制列可见性**：基于当前登录用户的角色和后端返回的字段权限规则，动态决定表格列的显示与隐藏。未授权的列应在列设置面板中不可选或置灰。
  - **按权限控制操作按钮**：在表格的“操作”列中，根据当前用户对特定数据记录的操作权限（如编辑、删除、查看详情等），动态控制操作按钮的可见性或禁用状态。
  - **敏感字段脱敏**：提供一种机制，允许根据用户权限或预设规则，对表格中特定列的敏感数据进行部分或完全脱敏处理（例如，手机号显示为 `138****1234`）。
  - **条件显示列**：支持配置基于行数据内容的条件表达式，当条件满足时才显示某列。例如，当订单状态为“已完成”时才显示“完成时间”列。
  - **不同用户保存个人视图**：允许每个用户保存多套自定义的表格视图，包括列的可见性、顺序、宽度、筛选、排序等配置。
  - **共享视图**：提供功能，允许用户将其创建的个人视图共享给指定的其他用户或用户组，并设置共享权限（只读/可编辑）。
  - **默认视图/团队视图**：支持管理员或特定角色用户设置某个视图为全局默认视图或团队默认视图。当用户首次访问或重置视图时，加载默认视图。
  - **操作审计**：记录用户对表格视图（创建、修改、删除、共享）和关键数据操作（如批量编辑、导出）的行为日志，以便追溯。

- **普通面板场景 vs 低代码场景的差异处理：**
  - **普通面板场景**：主要通过 Vue 组件的 Props 和 Composable 函数来传递和管理权限配置。前端组件将直接消费后端提供的权限数据，并根据这些数据渲染表格。视图管理功能将通过 `table-view-toolbar.vue` 进行增强。
  - **低代码场景（AMIS）**：需要将这些权限与配置能力映射到 AMIS Schema 的属性中。可能需要扩展 AMIS 的 `Table` 组件或注册自定义的 AMIS 插件，以便 AMIS 渲染器能够理解并应用这些权限规则。例如，通过 `column.permissionKey` 或 `action.permissionKey` 属性来关联权限。

### 三、核心数据模型 / 类型定义

为了支持上述封装目标，需要扩展现有的类型定义，特别是 `TableViewColumnConfig` 和 `TableViewConfig`，并引入新的权限相关类型。

```typescript
// types/api.ts 或 types/table-permissions.ts

/**
 * 字段权限规则定义
 * 后端接口 GET /dynamic-tables/{key}/field-permissions 返回的结构
 */
export interface FieldPermissionRule {
  fieldName: string; // 字段名称，对应 column.key
  roleCode: string;  // 角色编码，例如 'admin', 'user', 'guest'
  canView: boolean;  // 是否可见
  canEdit: boolean;  // 是否可编辑（用于脱敏或行内编辑权限）
  canExport: boolean; // 是否可导出
  maskingRule?: string; // 脱敏规则，例如 'phone', 'idCard', 'custom:***'
}

/**
 * 操作权限规则定义
 */
export interface ActionPermissionRule {
  actionKey: string; // 操作按钮的唯一标识，例如 'edit', 'delete', 'viewDetail'
  roleCode: string;  // 角色编码
  canAccess: boolean; // 是否可访问（可见且可用）
}

/**
 * 扩展 TableViewColumnConfig，增加权限和条件显示相关属性
 */
export interface TableViewColumnConfig extends OriginalTableViewColumnConfig {
  permissionKey?: string; // 关联的字段权限Key，用于列可见性控制
  conditionExpression?: string; // 条件显示列的表达式，例如 'record.status === "completed"'
  isSensitive?: boolean; // 是否是敏感字段
  maskingRule?: string; // 字段脱敏规则，优先级高于 FieldPermissionRule 中的规则
}

/**
 * 扩展 TableViewConfig，增加视图共享和团队视图相关属性
 */
export interface TableViewConfig extends OriginalTableViewConfig {
  isShared?: boolean; // 是否为共享视图
  sharedWith?: {
    users?: string[]; // 共享给的用户ID列表
    roles?: string[]; // 共享给的角色编码列表
    teams?: string[]; // 共享给的团队ID列表
  };
  isDefault?: boolean; // 是否为全局默认视图
  isTeamDefault?: boolean; // 是否为团队默认视图
  teamId?: string; // 团队ID
  auditLog?: AuditLogEntry[]; // 视图操作审计日志
}

/**
 * 视图操作审计日志条目
 */
export interface AuditLogEntry {
  timestamp: number; // 操作时间戳
  operatorId: string; // 操作用户ID
  action: 'create' | 'update' | 'delete' | 'share' | 'setDefault'; // 操作类型
  details?: Record<string, any>; // 操作详情
}

// 假设 OriginalTableViewColumnConfig 和 OriginalTableViewConfig 是现有定义
// import { TableViewColumnConfig as OriginalTableViewColumnConfig, TableViewConfig as OriginalTableViewConfig } from './api';
```

- **说明与现有 TableViewConfig 的集成方式：**
  上述定义通过接口继承（`extends`）的方式，在不破坏现有 `TableViewColumnConfig` 和 `TableViewConfig` 结构的基础上，增加了新的权限和配置相关属性。这意味着在保存或加载 `TableViewConfig` 时，这些新增的属性将作为其一部分进行持久化。后端 `TableView` API 需要同步更新以支持这些新增字段的存储和查询。

### 四、组件/Composable 封装方案

本节将详细说明如何通过新增或改造文件，实现权限与配置能力的封装。

- **新增或改造哪些文件：**
  - **`composables/useTablePermissions.ts` (新增)**：核心的权限处理 Composable，负责获取、解析和应用字段及操作权限。
  - **`composables/useTableView.ts` (改造)**：集成 `useTablePermissions`，在处理列配置时考虑权限因素。
  - **`components/table/table-view-toolbar.vue` (改造)**：列设置面板需要根据权限禁用或隐藏某些列选项。
  - **`components/table/TableView.vue` (改造)**：表格渲染组件，在渲染列和操作按钮时应用权限和脱敏逻辑。
  - **`types/table-permissions.ts` (新增)**：存放所有权限相关的类型定义。
  - **`services/api-permission.ts` (新增)**：如果字段权限接口独立于 `api-system.ts`，则需要新增此文件。

- **关键 Props/Emits/Expose 设计：**

  **`composables/useTablePermissions.ts`**
  ```typescript
  interface UseTablePermissionsOptions {
    tableKey: string; // 表格唯一标识
    currentUserRoles: string[]; // 当前用户角色列表
    // 更多上下文信息，如当前用户ID，租户ID等
  }

  interface ColumnPermission {
    key: string; // 列的key
    visible: boolean; // 是否可见
    canEdit: boolean; // 是否可编辑（用于脱敏判断）
    maskingRule?: string; // 脱敏规则
  }

  interface ActionPermission {
    key: string; // 操作按钮的key
    canAccess: boolean; // 是否可访问
  }

  function useTablePermissions(options: UseTablePermissionsOptions): {
    columnPermissions: Ref<ColumnPermission[]>;
    actionPermissions: Ref<ActionPermission[]>;
    getColumnPermission: (columnKey: string) => ColumnPermission | undefined;
    getActionPermission: (actionKey: string) => ActionPermission | undefined;
    applyMasking: (columnKey: string, value: any) => any; // 应用脱敏函数
  }
  ```

  **`components/table/TableView.vue` (改造)**
  ```vue
  <template>
    <a-table
      :columns="processedColumns" // 经过权限处理的列
      :data-source="processedDataSource" // 经过脱敏处理的数据源
      ...
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'actions'">
          <a-button v-if="getActionPermission('edit')?.canAccess" @click="edit(record)">编辑</a-button>
          <a-button v-if="getActionPermission('delete')?.canAccess" @click="del(record)">删除</a-button>
        </template>
        <template v-else-if="column.isSensitive && !getColumnPermission(column.key)?.canEdit">
          {{ applyMasking(column.key, record[column.key]) }}
        </template>
        <template v-else>
          {{ record[column.key] }}
        </template>
      </template>
    </a-table>
  </template>

  <script setup lang="ts">
  import { computed, ref } from 'vue';
  import { useTablePermissions } from '@/composables/useTablePermissions';
  import { useTableView } from '@/composables/useTableView';
  import { TableViewColumnConfig } from '@/types/table-permissions';

  const props = defineProps({
    tableKey: { type: String, required: true },
    columns: { type: Array as PropType<TableViewColumnConfig[]>, required: true },
    dataSource: { type: Array, required: true },
    currentUserRoles: { type: Array as PropType<string[]>, default: () => [] },
  });

  const { columnPermissions, actionPermissions, getColumnPermission, getActionPermission, applyMasking } = useTablePermissions({
    tableKey: props.tableKey,
    currentUserRoles: props.currentUserRoles,
  });

  const { currentTableViewConfig } = useTableView(props.tableKey, props.columns); // 假设 useTableView 内部会处理视图加载

  const processedColumns = computed(() => {
    return props.columns
      .filter(col => {
        // 1. 按角色控制列可见性
        const perm = getColumnPermission(col.key);
        const isVisibleByPermission = perm ? perm.visible : true; // 如果没有权限规则，默认可见

        // 2. 条件显示列
        let isVisibleByCondition = true;
        if (col.conditionExpression) {
          try {
            // 动态执行条件表达式，需要注意沙箱环境和安全性
            // 示例：eval(col.conditionExpression) 实际应使用更安全的表达式解析器
            // 暂时简化处理，实际需要更复杂的逻辑来解析和执行表达式
            // isVisibleByCondition = eval(col.conditionExpression.replace('record', 'props.dataSource[0]')); // 假设只检查第一行，实际需要遍历
          } catch (e) {
            console.error('Error evaluating condition expression:', e);
            isVisibleByCondition = false;
          }
        }
        return isVisibleByPermission && isVisibleByCondition && (currentTableViewConfig.value?.columns.find(c => c.key === col.key)?.visible ?? true);
      })
      .map(col => ({
        ...col,
        // 可以在这里处理列的标题，例如添加脱敏标识
      }));
  });

  const processedDataSource = computed(() => {
    return props.dataSource.map(record => {
      const newRecord = { ...record };
      props.columns.forEach(col => {
        if (col.isSensitive) {
          newRecord[col.key] = applyMasking(col.key, record[col.key]);
        }
      });
      return newRecord;
    });
  });

  // Expose for parent components if needed
  defineExpose({
    refreshPermissions: () => { /* re-fetch permissions */ },
  });
  </script>
  ```

- **核心实现逻辑说明：**
  1. **权限获取与缓存**：`useTablePermissions` Composable 在初始化时，根据 `tableKey` 和 `currentUserRoles` 调用后端接口（`GET /dynamic-tables/{key}/field-permissions` 和可能的 `GET /dynamic-tables/{key}/action-permissions`）获取当前用户在当前表格下的字段和操作权限规则。这些规则应被缓存，并在用户角色变化或权限刷新时更新。
  2. **列可见性控制**：`TableView.vue` 中的 `processedColumns` 计算属性会结合 `useTableView` 中用户自定义的列可见性配置和 `useTablePermissions` 返回的权限规则。只有当用户自定义为可见且权限允许可见时，列才会被渲染。列设置面板 (`table-view-toolbar.vue`) 也应使用 `getColumnPermission` 来禁用或隐藏用户无权查看的列选项。
  3. **操作按钮权限控制**：在 `TableView.vue` 的 `bodyCell` 插槽中，渲染操作按钮时，通过 `getActionPermission(actionKey)?.canAccess` 来判断按钮是否可见或可用。这要求每个操作按钮都有一个唯一的 `actionKey`。
  4. **敏感字段脱敏**：`useTablePermissions` 提供 `applyMasking` 函数，根据 `FieldPermissionRule` 或 `TableViewColumnConfig` 中定义的 `maskingRule` 对数据进行脱敏。`TableView.vue` 在渲染单元格内容时，如果列被标记为敏感且用户无权查看原始数据，则调用 `applyMasking` 进行处理。脱敏规则可以预定义（如手机号、身份证号）或支持自定义正则。
  5. **条件显示列**：`TableView.vue` 在 `processedColumns` 中增加逻辑，解析 `TableViewColumnConfig.conditionExpression`。这个表达式会在运行时根据当前行数据进行评估，决定列是否显示。为了安全性，建议使用安全的表达式解析库而非 `eval`。

### 五、与持久化视图的集成

权限与配置能力的状态需要与现有的 `TableViewConfig` 紧密集成，以实现视图的持久化和共享。

- **该维度的状态如何持久化到 TableViewConfig：**
  - **列权限相关**：`TableViewColumnConfig` 中新增的 `permissionKey`、`conditionExpression`、`isSensitive`、`maskingRule` 等属性将直接存储在 `TableViewConfig.columns` 数组中。这些属性是视图定义的一部分，当用户保存视图时，它们会随之保存。
  - **视图共享相关**：`TableViewConfig` 中新增的 `isShared`、`sharedWith`、`isDefault`、`isTeamDefault`、`teamId` 等属性将直接存储在 `TableViewConfig` 根级别。这些属性定义了视图的共享范围和默认状态。
  - **操作审计**：`TableViewConfig` 可以包含一个 `auditLog` 数组，记录视图本身的生命周期操作。更细粒度的数据操作审计通常由后端服务负责，但前端可以触发后端审计接口。

- **自动保存 vs 手动保存策略：**
  - **自动保存**：`useTableView.ts` 已有 `scheduleAutoSave` (400ms 防抖) 机制。对于用户在列设置面板中进行的列可见性、顺序调整等操作，以及视图共享状态的修改，可以沿用自动保存策略，确保用户配置的实时性。但对于“设为默认视图”或“共享视图”这类影响范围较大的操作，应提供明确的用户确认。
  - **手动保存**：对于“另存为新视图”或首次创建视图，以及修改共享视图的共享范围等重要操作，应强制用户进行手动保存，并提供视图名称、描述、共享范围等配置项。
  - **权限规则本身不应由前端保存**：字段权限规则 (`FieldPermissionRule`, `ActionPermissionRule`) 是由后端管理和提供的，前端只负责消费和应用，不应将其作为 `TableViewConfig` 的一部分进行持久化。

### 六、低代码（AMIS）集成方案

在 AMIS 低代码场景下，需要确保这些权限与配置能力能够通过 AMIS Schema 进行声明和渲染。

- **如何在 AMIS Schema 中支持该能力：**
  - **扩展 AMIS Table 列属性**：AMIS 的 `Table` 组件的 `columns` 属性可以接受自定义属性。我们可以在 `DynamicColumnDef` 或 AMIS Schema 的 `column` 定义中增加类似 `permissionKey`、`conditionExpression`、`isSensitive`、`maskingRule` 等属性。
    ```json
    {
      "type": "table",
      "columns": [
        {
          "name": "id",
          "label": "ID"
        },
        {
          "name": "phone",
          "label": "手机号",
          "permissionKey": "user_phone_view", // 关联权限Key
          "isSensitive": true,
          "maskingRule": "phone"
        },
        {
          "name": "finishTime",
          "label": "完成时间",
          "conditionExpression": "data.status === 'completed'" // 条件显示
        },
        {
          "type": "operation",
          "label": "操作",
          "buttons": [
            {
              "type": "button",
              "label": "编辑",
              "actionKey": "edit_user", // 关联操作权限Key
              "level": "link"
            }
          ]
        }
      ]
    }
    ```
  - **自定义表达式上下文**：AMIS 支持表达式，`conditionExpression` 可以直接利用 AMIS 的表达式能力。但需要确保表达式中能够访问到当前行数据 (`data`) 和当前用户权限信息。
  - **权限数据注入**：在 `AmisRenderer.vue` 渲染 AMIS Schema 之前，需要将当前用户的角色和权限数据注入到 AMIS 的全局上下文中，或者通过自定义的 `fetcher` 或 `adapter` 机制，让 AMIS 组件能够获取到这些权限信息。

- **是否需要注册自定义 AMIS 插件：**
  - **是，可能需要。** 尽管可以通过扩展 Schema 属性来传递配置，但为了在 AMIS 渲染层真正“应用”这些权限和脱敏逻辑，可能需要注册自定义的 AMIS 插件或自定义组件。
  - **自定义插件的作用**：
    - **列渲染插件**：拦截 AMIS `Table` 组件的列渲染逻辑，根据 `permissionKey` 和 `conditionExpression` 动态调整列的可见性。对于敏感字段，插件可以在渲染前应用 `maskingRule` 进行脱敏。
    - **操作按钮插件**：拦截 AMIS `Operation` 列的按钮渲染，根据 `actionKey` 和用户权限动态控制按钮的显示或禁用状态。
    - **视图管理插件**：如果要在 AMIS 页面中集成“保存视图”、“共享视图”等功能，可能需要一个自定义的 AMIS 组件来封装 `table-view-toolbar.vue` 的功能，并与 `useTableView` 提供的 API 交互。
  - **`business-plugins.ts`**：可以在此文件中注册新的 AMIS 插件，例如 `atlas-permission-table-column` 或 `atlas-masked-text`，以实现上述功能。

### 七、优先级与实施建议

本维度涉及表格的核心安全与用户体验，建议分阶段实施。

- **基础版/进阶版/高阶版分级：**
  - **基础版 (P0)**：
    - **按角色控制列可见性**：集成后端字段权限接口，在 `useTablePermissions` 中获取列权限，并在 `TableView.vue` 和列设置面板中应用。
    - **按权限控制操作按钮**：为操作按钮增加 `actionKey` 属性，并在 `TableView.vue` 中根据权限控制其可见性/可用性。
    - **个人视图保存与加载**：确保用户可以保存和加载个人自定义视图（已部分实现，需确保权限相关配置能持久化）。
    - **AMIS 集成**：通过扩展 Schema 属性，在 AMIS `Table` 组件中支持 `permissionKey` 和 `actionKey`，并利用 AMIS 的 `visibleOn` 表达式能力实现基础的可见性控制。
  - **进阶版 (P1)**：
    - **敏感字段脱敏**：实现通用的脱敏函数 `applyMasking`，支持预定义规则和自定义规则，并在 `TableView.vue` 中应用。
    - **条件显示列**：实现安全的表达式解析器，支持 `conditionExpression` 动态控制列可见性。
    - **默认视图/团队视图**：实现设置全局/团队默认视图的功能，并在视图加载时优先应用。
    - **视图共享**：实现视图的共享功能，包括共享给用户/角色/团队，并管理共享权限。
    - **AMIS 集成**：开发自定义 AMIS 插件，更优雅地处理列权限、脱敏和条件显示。
  - **高阶版 (P2)**：
    - **操作审计**：集成后端审计服务，记录视图配置和关键数据操作日志。
    - **更复杂的权限模型**：支持行级别、单元格级别的更细粒度权限控制。
    - **权限配置 UI**：提供前端界面，允许管理员配置字段和操作权限规则。

- **预估工作量（人天）：**
  - **基础版**：约 **10-15 人天**。主要工作量在于 `useTablePermissions` 的实现、与 `TableView.vue` 和 `table-view-toolbar.vue` 的集成，以及 AMIS 基础属性扩展。
  - **进阶版**：约 **15-20 人天**。涉及脱敏规则引擎、安全表达式解析、视图共享逻辑和 AMIS 插件开发，复杂度较高。
  - **高阶版**：约 **5-10 人天**。主要为后端审计接口的集成和前端 UI 的开发。
  - **总计**：约 **30-45 人天**。

- **关键风险与注意事项：**
  - **安全性**：权限控制是核心安全功能，必须确保后端权限接口的健壮性，前端权限判断不可绕过。敏感字段脱敏需严格验证规则，防止数据泄露。
  - **性能**：权限规则的获取和应用可能对表格渲染性能产生影响，特别是当规则复杂或数据量大时。需要考虑权限数据的缓存策略和高效的规则匹配算法。
  - **表达式安全性**：`conditionExpression` 的解析和执行必须在安全的沙箱环境中进行，避免任意代码执行漏洞。
  - **AMIS 兼容性**：自定义 AMIS 插件需要密切关注 AMIS 版本的兼容性，确保升级时不会出现问题。
  - **用户体验**：权限控制应在不影响用户正常操作流程的前提下进行。例如，无权查看的列应直接隐藏，而不是显示错误信息。视图共享和默认视图的设置流程应清晰易懂。
  - **测试**：权限相关功能需要进行全面的单元测试、集成测试和端到端测试，覆盖不同角色、不同权限组合下的各种场景。


---

## 维度 9：导入导出能力

- **现状评估**：部分
- **实施优先级**：P0
- **预估人天**：12 天

### 一、现状分析

- **代码库中已有哪些相关实现（文件路径 + 功能说明）**

  根据 `codebase_summary.md` 文件，当前代码库中存在一个与导入导出相关的实现：

  - **`composables/useExcelExport.ts`** [1]
    - **功能说明**：该文件提供了一套针对用户模块的 Excel 导入导出功能。具体包括：
      - `exportUsers(keyword)`: 用于导出用户数据到 Excel 文件。
      - `downloadImportTemplate()`: 用于下载用户导入的模板文件。
      - `importUsers(file)`: 用于导入用户数据。
      - `ImportResult` 类型定义：包含了导入结果的统计信息（总行数、成功数、失败数、错误详情）。

- **存在哪些缺口和不足**

  `codebase_summary.md` 中明确指出，现有能力存在以下缺口：

  - **通用化不足** [1]：`composables/useExcelExport.ts` 中的实现是针对特定业务模块（用户模块）的，不具备通用性。这意味着其他业务模块如果需要导入导出功能，需要重复开发或复制类似代码。
  - **功能不完善**：
    - **导出方面**：目前仅支持导出用户数据，未涵盖导出 CSV 格式、导出当前页数据、导出勾选项数据、导出当前筛选结果等通用导出场景。
    - **导入方面**：虽然提供了导入用户数据的功能，但对于导入校验的通用机制、导入错误反馈的标准化处理、以及模板下载的通用化支持方面，缺乏统一的封装。
  - **与表格组件集成度低**：现有的导入导出功能与核心表格组件（如 `useTableView` 或 `useCrudPage`）的集成度不高，无法直接利用表格的当前状态（如筛选条件、分页信息、选中行）进行灵活的导入导出操作。

[1]: /home/ubuntu/codebase_summary.md "SecurityPlatform 代码库摘要（供并行分析使用）"

### 二、封装目标

该维度的封装目标是提供一套通用、可配置的表格数据导入导出解决方案，以满足不同业务场景的需求，并与现有表格组件（`useTableView`、`useCrudPage`）及低代码平台（AMIS）无缝集成。具体封装目标如下：

- **该维度需要封装哪些核心能力（对照用户需求清单逐条说明）**

  | 需求清单项 | 封装目标说明 |
| :--- | :--- |
| **导出 Excel/CSV** | 提供统一的导出函数，支持将表格数据导出为 Excel (`.xlsx`) 和 CSV (`.csv`) 两种主流格式。 |
| **导出当前页/全量** | 允许用户选择导出范围：仅导出当前分页的数据，或根据当前筛选条件导出所有分页的全部数据。 |
| **导出勾选项** | 支持导出用户在表格中手动勾选的行数据。 |
| **导出当前筛选结果** | 导出操作应默认使用表格当前的筛选条件，确保导出的数据与用户在界面上看到的数据范围一致。 |
| **导入 Excel** | 提供通用的 Excel 文件导入功能，支持将外部数据批量导入到系统中。 |
| **导入校验** | 在导入过程中，支持对数据进行前端和后端的双重校验。前端校验可用于基本格式检查，后端校验用于业务逻辑验证。 |
| **导入错误反馈** | 导入完成后，提供清晰、详细的错误反馈报告，包括成功/失败的行数统计、具体的错误行及原因，方便用户修正后重新导入。 |
| **模板下载** | 提供与导入功能配套的 Excel 模板下载功能，模板中应包含正确的列头和数据格式说明，引导用户填写正确的数据。 |

- **普通面板场景 vs 低代码场景的差异处理**

  | 场景 | 差异处理说明 |
| :--- | :--- |
| **普通面板场景** | 在 Vue 组件中直接使用封装好的 Composable (`useTableImportExport`)，通过函数调用和参数传递的方式实现导入导出功能。开发者可以完全控制导入导出的行为和 UI 表现。 |
| **低代码（AMIS）场景** | 将导入导出功能封装成 AMIS 自定义组件或插件，通过在 AMIS Schema 中配置 `type` 和 `props` 来启用。需要解决 AMIS 上下文与 Vue 组件状态的同步问题，例如将 AMIS CRUD 组件的查询参数和选中行数据传递给 Vue 组件。 |

### 三、核心数据模型 / 类型定义

为了支持通用的导入导出能力，我们需要在 `typings/table.d.ts` 中扩展 `TableViewConfig`，并定义新的接口和类型。

- **给出 TypeScript 接口/类型定义（代码块）**

  ```typescript
  // in: typings/table.d.ts

  /**
   * 导出配置
   */
  export interface TableExportConfig {
    /** 导出文件名 (不含扩展名) */
    fileName?: string;
    /** 导出API，用于后端导出 */
    exportApi?: (params: Record<string, any>) => Promise<any>;
    /** 自定义前端导出逻辑 */
    onExport?: (scope: 'all' | 'current' | 'selected', format: 'excel' | 'csv') => Promise<void>;
  }

  /**
   * 导入配置
   */
  export interface TableImportConfig {
    /** 模板下载地址或处理函数 */
    templateDownloadUrl?: string | (() => Promise<void>);
    /** 导入API */
    importApi?: (file: File) => Promise<ImportResult>;
    /** 导入成功后的回调 */
    onSuccess?: (result: ImportResult) => void;
  }

  /**
   * 导入结果
   */
  export interface ImportResult {
    total: number;
    success: number;
    failure: number;
    errors?: { rowIndex: number; message: string }[];
  }

  // 扩展 TableViewConfig
  export interface TableViewConfig<T = any> {
    // ... existing properties

    /** 导出配置 */
    exportConfig?: TableExportConfig;

    /** 导入配置 */
    importConfig?: TableImportConfig;
  }
  ```

- **说明与现有 TableViewConfig 的集成方式**

  通过在 `TableViewConfig` 中增加 `exportConfig` 和 `importConfig` 两个可选属性，我们可以为每个表格实例按需配置导入导出功能。这种方式具有良好的扩展性和灵活性：

  - **按需开启**：只有当 `exportConfig` 或 `importConfig` 被提供时，表格才会渲染对应的导入导出按钮或功能入口。
  - **配置驱动**：导入导出的具体行为（如 API 地址、文件名、回调函数）都通过配置来定义，使得业务逻辑与组件实现解耦。
  - **类型安全**：TypeScript 的类型定义确保了开发者在配置时能够获得良好的代码提示和编译时检查。

### 四、组件/Composable 封装方案

为了实现通用化的导入导出能力，我们将改造现有的 `useExcelExport.ts`，并创建新的 UI 组件来承载交互。

- **新增或改造哪些文件（给出文件路径建议）**

  | 文件路径 | 说明 |
| :--- | :--- |
| `composables/useTableImportExport.ts` | **改造** `useExcelExport.ts` 为通用的 Composable，处理导入导出的核心逻辑，不再局限于用户模块。 |
| `components/common/TableExportAction.vue` | **新增** UI 组件，提供导出按钮和下拉菜单，让用户选择导出范围和格式。 |
| `components/common/TableImportAction.vue` | **新增** UI 组件，提供导入按钮、文件上传、进度显示和错误反馈对话框。 |

- **关键 Props/Emits/Expose 设计（代码块）**

  **`useTableImportExport.ts`**

  ```typescript
  import { TableViewConfig, ImportResult } from '@/typings/table';

  export function useTableImportExport(config: Ref<TableViewConfig>) {
    const { exportConfig, importConfig } = config.value;

    // 导出逻辑
    const handleExport = async (scope: 'all' | 'current' | 'selected', format: 'excel' | 'csv') => {
      // ... 实现导出逻辑，调用 exportApi 或 onExport
    };

    // 导入逻辑
    const handleImport = async (file: File): Promise<ImportResult> => {
      // ... 实现导入逻辑，调用 importApi
    };

    // 模板下载逻辑
    const handleTemplateDownload = async () => {
      // ... 实现模板下载逻辑
    };

    return {
      handleExport,
      handleImport,
      handleTemplateDownload,
    };
  }
  ```

  **`TableExportAction.vue`**

  ```typescript
  // Props
  interface Props {
    tableKey: string; // 用于持久化
    getQueryParams: () => Record<string, any>;
    getSelectedRowKeys: () => (string | number)[];
  }

  // Emits
  // (无)

  // Expose
  // (无)
  ```

  **`TableImportAction.vue`**

  ```typescript
  // Props
  interface Props {
    tableKey: string;
    importOptions: TableImportConfig;
  }

  // Emits
  interface Emits {
    (e: 'import-success', result: ImportResult): void;
  }

  // Expose
  // (无)
  ```

- **核心实现逻辑说明**

  1.  **`useTableImportExport.ts`** 将作为核心逻辑层，接收 `TableViewConfig`，并根据配置提供 `handleExport`、`handleImport` 和 `handleTemplateDownload` 等方法。
  2.  **`TableExportAction.vue`** 组件内部会维护一个下拉菜单，包含“导出全部”、“导出当前页”、“导出选中项”等选项。点击后，它会调用 `getQueryParams` 和 `getSelectedRowKeys` 获取当前表格状态，然后调用 `useTableImportExport` 中的 `handleExport` 方法执行导出。
  3.  **`TableImportAction.vue`** 组件提供一个上传按钮。用户选择文件后，组件会调用 `handleImport` 方法发起导入请求。导入过程中显示加载状态，导入完成后，如果 `importApi` 返回错误信息，则弹出一个对话框展示 `ImportResult` 中的错误详情，并触发 `import-success` 事件通知父组件刷新表格。

### 五、与持久化视图的集成

导入导出能力本身通常不涉及需要持久化的状态，因为它们是即时操作。然而，与导出相关的某些用户偏好可以考虑持久化。

- **该维度的状态如何持久化到 TableViewConfig**

  - **导出格式偏好**：用户上次选择的导出格式（`excel` 或 `csv`）可以被记录下来。这可以作为一个本地状态存储在 `localStorage` 中，与 `tableKey` 关联，而不是直接存入后端的 `TableViewConfig`，因为它更多是客户端的用户偏好。
  - **导入/导出列选择**：如果未来支持用户自定义导入导出的列，那么这些列的选择状态应该被持久化。这个状态可以作为 `exportConfig` 或 `importConfig` 的一部分，存储在 `TableViewConfig` 中，这样团队成员之间可以共享相同的导入导出列设置。

- **自动保存 vs 手动保存策略**

  - **自动保存**：对于导出格式偏好这类轻量级状态，采用自动保存策略是合适的。当用户选择一个新的导出格式后，立即将其写入 `localStorage`。
  - **手动保存**：对于可能引入的自定义列选择功能，应采用手动保存策略。用户在配置界面完成列选择后，点击“保存”按钮，将配置更新到 `TableViewConfig` 中。这为用户提供了一个明确的确认步骤，避免误操作。

### 六、低代码（AMIS）集成方案

在 SecurityPlatform 项目中，AMIS 作为低代码引擎，承载了动态表单和 CRUD 页面的快速构建。为了在 AMIS 中支持导入导出能力，我们需要考虑如何将封装好的 Vue Composable 和组件集成到 AMIS Schema 中。

- **如何在 AMIS Schema 中支持该能力**

  AMIS 提供了多种方式来扩展其功能，包括自定义组件、自定义动作等。对于导入导出能力，可以采用以下策略：

  1.  **通过自定义组件集成**：
      - 将 `table-export-action.vue` 和 `table-import-action.vue` 封装成 AMIS 自定义组件。这通常涉及到在 Vue 组件外部包裹一层 React 组件（因为 AMIS 基于 React），或者直接通过 AMIS 的 `custom` 渲染器来渲染 Vue 组件 [1]。
      - 在 AMIS Schema 中，可以通过 `"type": "custom"` 或注册的自定义组件类型来引用这些导入导出组件。例如：

      ```json
      {
        "type": "page",
        "body": [
          {
            "type": "crud",
            "api": "/api/users",
            "columns": [
              // ... 表格列定义
            ],
            "toolbar": [
              // ... 其他工具栏按钮
              {
                "type": "custom",
                "name": "tableExportAction",
                "component": "TableExportAction", // 引用注册的自定义组件
                "props": {
                  "tableKey": "users",
                  "getQueryParams": "${getCrudQueryParams}", // 假设AMIS能提供上下文参数
                  "getSelectedRowKeys": "${getCrudSelectedRowKeys}"
                }
              },
              {
                "type": "custom",
                "name": "tableImportAction",
                "component": "TableImportAction", // 引用注册的自定义组件
                "props": {
                  "tableKey": "users",
                  "importOptions": {
                    "templateDownloadUrl": "/api/users/template"
                  }
                }
              }
            ]
          }
        ]
      }
      ```

      - **参数传递**：关键在于如何将 AMIS CRUD 组件的当前状态（如查询参数、选中行）传递给 Vue 导入导出组件。这可能需要 AMIS 的上下文变量 (`${xxx}`) 或通过自定义组件的 `data` 属性进行传递。`getQueryParams` 和 `getSelectedRowKeys` 可以是 AMIS 上下文中的函数或通过事件触发。

  2.  **通过自定义动作 (Action) 集成**：
      - 如果导入导出操作逻辑相对简单，也可以考虑将其封装为 AMIS 的自定义动作。例如，点击导出按钮时触发一个自定义动作，该动作在前端调用 `useTableExport` 的逻辑。
      - 这种方式可能更适合简单的导出场景，对于需要复杂 UI 交互（如选择导出范围、格式）的场景，自定义组件会更灵活。

- **是否需要注册自定义 AMIS 插件**

  **是，需要注册自定义 AMIS 插件。**

  为了让 AMIS 能够识别并渲染我们封装的 Vue 导入导出组件，我们需要在 `business-plugins.ts` [1] 中注册自定义的 AMIS 渲染器或组件。具体步骤如下：

  1.  **注册自定义渲染器**：在 AMIS 运行时中，注册一个自定义渲染器，将 AMIS Schema 中的特定 `type` 映射到我们的 Vue 组件。例如，可以注册 `TableExportAction` 和 `TableImportAction` 作为 AMIS 的组件。
  2.  **提供数据上下文**：自定义插件还需要处理如何从 AMIS 的上下文环境中获取表格的查询参数、分页信息和选中行数据，并将其作为 `props` 传递给 Vue 组件。
  3.  **事件处理**：确保 Vue 组件内部触发的事件（如导入成功、导入失败）能够被 AMIS 捕获并处理，以便 AMIS 页面能够根据导入导出结果进行刷新或显示通知。

  通过注册自定义 AMIS 插件，我们可以实现 Vue 组件与 AMIS 框架的深度融合，使得导入导出能力在低代码平台中也能得到一致且强大的支持。

[1]: /home/ubuntu/codebase_summary.md "SecurityPlatform 代码库摘要（供并行分析使用）"

### 七、优先级与实施建议

- **基础版/进阶版/高阶版分级**

  为了逐步实现通用化的导入导出能力，建议分阶段实施：

  | 版本 | 功能描述 | 预估工作量 |
| :--- | :--- | :--- |
| **基础版 (P0)** | - 封装 `useTableImportExport` Composable。<br>- 实现通用的 Excel/CSV 导出功能（全量、当前页、选中项）。<br>- 实现通用的 Excel 导入功能和模板下载。 | 5 人天 |
| **进阶版 (P1)** | - 实现导入错误的可视化反馈界面。<br>- 封装 `TableExportAction` 和 `TableImportAction` Vue 组件。<br>- 在普通面板场景中集成和应用。 | 3 人天 |
| **高阶版 (P2)** | - 开发并注册 AMIS 自定义插件，将导入导出组件集成到低代码平台。<br>- 实现 AMIS Schema 与 Vue 组件的状态同步。<br>- 完善持久化策略，支持用户偏好设置。 | 4 人天 |

- **预估工作量（人天）**

  - **总计**：12 人天
  - **详细分工**：
    - **后端** (2 人天): 提供通用的导入导出 API 接口，支持根据查询参数导出数据，并处理导入数据的校验和持久化。
    - **前端** (10 人天): 完成上述基础版、进阶版和高阶版的全部前端开发工作。

- **关键风险与注意事项**

  - **大数量导出性能**：当导出全量数据时，如果数据量非常大（例如超过 10 万行），直接在前端生成 Excel 文件可能会导致浏览器卡顿或内存溢出。对于这种情况，`exportApi` 应设计为异步任务，后端生成文件后提供下载链接，而不是同步返回文件流。
  - **导入数据校验复杂度**：不同业务场景的导入校验逻辑可能差异很大。通用导入功能应侧重于提供一个灵活的校验和错误反馈框架，而不是试图覆盖所有业务规则。复杂的校验逻辑应由后端 `importApi` 实现。
  - **AMIS 集成技术细节**：将 Vue 组件集成到基于 React 的 AMIS 中需要解决跨框架组件通信和状态管理的问题。需要投入时间研究 AMIS 的插件机制和 `amis-vue` 等社区方案，确保集成的稳定性和可维护性。
  - **依赖库选型**：前端 Excel/CSV 生成可以考虑使用 `exceljs`、`xlsx` 或 `papaparse` 等成熟库。需要评估它们的功能、性能和社区活跃度，选择最适合项目的方案。

---

## 维度 10：国际化与可访问性

- **现状评估**：部分
- **实施优先级**：P0
- **预估人天**：18 天

### 一、现状分析
- **代码库中已有哪些相关实现（文件路径 + 功能说明）**
  - **技术栈**: 项目已集成 `vue-i18n 9.x`，支持 `zh-CN/en-US` 双语。这意味着在文本内容的国际化方面有基础支持，表格的列头、提示信息、操作按钮等文本内容可以通过 `vue-i18n` 进行多语言处理。
  - **`types/api.ts`**: `TableViewColumnConfig` 中应包含 `label` 字段，该字段在实际使用中通常会传入国际化 key 或已翻译的字符串，但具体实现细节需进一步确认。
  - **`components/table/table-view-toolbar.vue`**: 工具栏中的文本内容（如“视图切换”、“保存”、“列设置”等）应已通过 `vue-i18n` 进行国际化处理。
  - **Ant Design Vue 4.x**: 作为基础组件库，Ant Design Vue 自身提供了国际化配置（`ConfigProvider`）以支持组件内部的文本、日期、数字等本地化显示，以及部分可访问性特性（如键盘导航、焦点管理）。

- **存在哪些缺口和不足**
  - **多语言表头**: 虽然有 `vue-i18n`，但表格列头的国际化具体实现方式（是直接传入翻译后的字符串还是传入 i18n key）需要明确，并确保与 `TableViewConfig` 的集成方式一致。
  - **日期/数字/货币本地化**: Ant Design Vue 提供了基础能力，但项目层面是否统一配置并应用于表格中的所有数据展示，以及是否支持基于列配置的自定义格式化（例如，某一列固定显示为美元，另一列显示为欧元），仍是待完善的细节。
  - **RTL (Right-to-Left) 支持**: 代码库摘要中未提及对 RTL 语言（如阿拉伯语、希伯来语）的支持。这通常需要全局的 CSS 样式调整和组件库的 RTL 模式支持，并可能影响表格布局和滚动方向。
  - **屏幕阅读器 (Screen Reader) 支持**: 未提及相关的 ARIA 属性或语义化 HTML 结构优化。表格的复杂结构（如合并单元格、嵌套表格、可编辑单元格）需要额外的 ARIA 属性来确保屏幕阅读器能正确解析内容和交互。
  - **键盘可操作性 (Keyboard Operability)**: 摘要中明确指出“11. 键盘导航 - 未实现”，这是可访问性的一个重要缺失。表格的焦点管理、Tab 键导航、方向键操作、快捷键操作等均未实现，严重影响无鼠标用户的体验。
  - **Focus 态**: 与键盘可操作性相关，组件的焦点状态（`focus` 样式）在自定义组件和复杂交互中可能需要额外处理，以确保视觉上的清晰指示。
  - **高对比度模式**: 未提及对高对比度模式的支持。这通常需要额外的 CSS 样式或主题配置，以确保在不同对比度设置下表格内容依然清晰可读。

### 二、封装目标
- **该维度需要封装哪些核心能力（对照用户需求清单逐条说明）**
  - **多语言表头**: 支持通过 `i18n key` 或函数动态生成列头文本，并能与 `TableViewConfig` 中的列配置无缝集成。
  - **日期/数字/货币本地化**: 提供统一的表格数据格式化能力，支持根据当前语言环境自动本地化日期、数字和货币，并允许通过列配置进行细粒度的格式化定制。
  - **RTL 支持**: 提供配置选项以启用或禁用 RTL 模式，确保表格布局、文本方向、滚动行为等在 RTL 语言环境下正确显示。
  - **屏幕阅读器支持**: 自动为表格元素（`<table>`, `<th>`, `<td>`）添加必要的语义化 HTML 属性和 ARIA 属性（如 `aria-label`, `aria-describedby`, `role`），特别针对复杂表格结构（如可排序列、可筛选列、可编辑单元格、合并单元格）进行优化。
  - **键盘可操作性**: 实现完整的键盘导航功能，包括：
    - Tab 键在可交互元素（如排序、筛选、编辑按钮）之间切换焦点。
    - 方向键在表格单元格之间移动焦点。
    - Enter/Space 键激活当前焦点元素（如编辑单元格、触发排序）。
    - Esc 键取消编辑或关闭弹窗。
  - **Focus 态**: 确保所有可交互元素在获得焦点时有清晰的视觉指示（如边框、背景色变化），并符合 WCAG 2.1 AA 级别要求。
  - **高对比度模式**: 提供主题或样式变量，支持在高对比度模式下自动调整表格的颜色方案，确保文本和背景之间有足够的对比度。

- **普通面板场景 vs 低代码场景的差异处理**
  - **普通面板场景**: 在 Vue 组件中直接使用封装的 Composable 或组件，通过 Props 传入国际化配置、可访问性属性等。开发者可以完全控制渲染逻辑和属性。
  - **低代码场景**: 需要将国际化和可访问性能力通过 AMIS Schema 进行声明式配置。这意味着需要在 AMIS Schema 中定义相应的字段（如 `i18nKey`、`locale`、`rtl`、`ariaLabel` 等），并在自定义渲染器或插件中解析这些配置，将其转换为 Ant Design Vue 表格组件可识别的 Props。

### 三、核心数据模型 / 类型定义

```typescript
// types/table-accessibility.d.ts

/**
 * 表格国际化配置
 */
export interface TableI18nConfig {
  headerKey?: string; // 列头国际化key
  formatter?: 'date' | 'number' | 'currency' | ((value: any, record: any) => string); // 数据格式化器
  formatOptions?: Intl.NumberFormatOptions | Intl.DateTimeFormatOptions; // 格式化选项
}

/**
 * 表格可访问性配置
 */
export interface TableAccessibilityConfig {
  enableRTL?: boolean; // 是否启用RTL模式
  ariaLabel?: string; // 表格整体的aria-label
  ariaDescribedBy?: string; // 表格整体的aria-describedby
  enableKeyboardNavigation?: boolean; // 是否启用键盘导航
  enableHighContrastMode?: boolean; // 是否启用高对比度模式
  columnAriaLabels?: Record<string, string>; // 列的aria-label映射
}

/**
 * 扩展 TableViewColumnConfig 以支持国际化和可访问性
 */
export interface TableViewColumn extends TableViewColumnConfig {
  i18n?: TableI18nConfig; // 列的国际化配置
  accessibility?: {
    ariaLabel?: string; // 列头的aria-label
    cellFormatter?: (value: any, record: any) => string; // 单元格内容的屏幕阅读器格式化
  }; // 列的可访问性配置
}

/**
 * 扩展 TableViewConfig 以支持国际化和可访问性
 */
export interface TableViewConfig {
  // ... 现有属性
  locale?: string; // 表格当前的语言环境，例如 'zh-CN', 'en-US'
  accessibility?: TableAccessibilityConfig; // 表格整体的可访问性配置
}
```

- **说明与现有 TableViewConfig 的集成方式**
  - 在 `TableViewColumn` 中新增 `i18n` 属性，用于配置列头的国际化 key 和列数据的格式化方式。`accessibility` 属性用于配置列级别的 ARIA 属性和屏幕阅读器格式化。
  - 在 `TableViewConfig` 中新增 `locale` 属性，用于指定表格整体的语言环境，这可以覆盖或补充 Ant Design Vue 的全局 `ConfigProvider` 配置。新增 `accessibility` 属性，用于配置表格整体的可访问性特性，如 RTL、键盘导航等。
  - 这样设计可以在 `TableViewConfig` 中集中管理表格的国际化和可访问性配置，方便持久化和低代码集成。

### 四、组件/Composable 封装方案

- **新增或改造哪些文件（给出文件路径建议）**
  - **`composables/useTableI18n.ts` (新增)**: 负责处理表格内部的国际化逻辑，包括列头翻译、数据格式化等。
  - **`composables/useTableAccessibility.ts` (新增)**: 负责处理表格的可访问性逻辑，包括 ARIA 属性注入、键盘导航事件监听、焦点管理、RTL 模式切换等。
  - **`components/table/TableView.vue` (改造)**: 作为核心表格组件，集成 `useTableI18n` 和 `useTableAccessibility`。
  - **`components/table/table-view-toolbar.vue` (改造)**: 在工具栏中增加国际化和可访问性相关的配置入口（如语言切换、RTL 切换、高对比度模式切换）。
  - **`types/api.ts` / `types/table-accessibility.d.ts` (新增/改造)**: 增加上述数据模型定义。

- **关键 Props/Emits/Expose 设计（代码块）**

  ```typescript
  // components/table/TableView.vue (部分 Props)
  interface TableViewProps {
    // ... 现有 Props
    config: TableViewConfig; // 包含国际化和可访问性配置的 TableViewConfig
    i18nMessages?: Record<string, any>; // 额外的国际化消息，用于表格内部的自定义文本
    onLocaleChange?: (locale: string) => void; // 语言环境切换事件
    onRTLChange?: (isRTL: boolean) => void; // RTL模式切换事件
  }

  // composables/useTableAccessibility.ts (部分 Expose)
  interface UseTableAccessibilityExpose {
    focusCell: (rowIndex: number, colKey: string) => void; // 暴露给外部，用于聚焦特定单元格
    toggleRTL: (enable?: boolean) => void; // 切换RTL模式
    toggleHighContrastMode: (enable?: boolean) => void; // 切换高对比度模式
  }
  ```

- **核心实现逻辑说明**
  - **`useTableI18n.ts`**: 
    - 接收 `TableViewConfig.locale` 和 `TableViewColumn.i18n`。
    - 提供 `getTranslatedHeader(column: TableViewColumn)` 方法，根据 `column.i18n.headerKey` 或 `column.label` 返回翻译后的列头。
    - 提供 `formatCellValue(value, column)` 方法，根据 `column.i18n.formatter` 和 `formatOptions` 对单元格数据进行本地化格式化。
    - 利用 `vue-i18n` 的 `useI18n` 组合式函数进行实际的翻译。
  - **`useTableAccessibility.ts`**: 
    - 接收 `TableViewConfig.accessibility` 配置。
    - **RTL 支持**: 监听 `enableRTL` 变化，动态添加/移除 `html` 元素的 `dir="rtl"` 属性或根元素的 CSS class，并可能需要调整 Ant Design Vue 的 `ConfigProvider`。
    - **键盘导航**: 监听 `keydown` 事件，实现 Tab 键、方向键的焦点管理逻辑。维护一个内部状态来跟踪当前焦点单元格或可交互元素。对于 Ant Design Vue 的 `a-table`，可能需要自定义 `customRow` 和 `customCell` 属性来注入 `tabindex` 和事件监听器。
    - **屏幕阅读器**: 根据 `TableViewColumn.accessibility` 和表格的整体结构，动态为 `<th>`, `<td>` 元素添加 `aria-label`, `aria-describedby` 等属性。对于可排序、可筛选的列，需要添加 `aria-sort`, `aria-haspopup` 等。
    - **Focus 态**: 确保通过键盘导航获得的焦点元素有清晰的 `:focus` 样式，可能需要覆盖 Ant Design Vue 的默认样式或在自定义组件中实现。
    - **高对比度模式**: 监听 `enableHighContrastMode` 变化，动态加载高对比度主题 CSS 或切换 CSS 变量。
  - **`TableView.vue`**: 
    - 引入 `useTableI18n` 和 `useTableAccessibility`。
    - 将 `config.locale` 传递给 `useTableI18n`。
    - 将 `config.accessibility` 传递给 `useTableAccessibility`。
    - 在渲染列头时，使用 `useTableI18n` 提供的翻译函数。
    - 在渲染单元格内容时，使用 `useTableI18n` 提供的格式化函数。
    - 在渲染表格结构时，根据 `useTableAccessibility` 提供的属性和事件监听器，注入 ARIA 属性和键盘导航逻辑。

### 五、与持久化视图的集成

- **该维度的状态如何持久化到 TableViewConfig**
  - **国际化**: `TableViewConfig` 中的 `locale` 字段可以直接持久化，表示用户为该视图选择的语言环境。`TableViewColumn` 中的 `i18n.headerKey` 和 `i18n.formatter`/`formatOptions` 也应作为列配置的一部分进行持久化。
  - **可访问性**: `TableViewConfig` 中的 `accessibility` 对象可以持久化，包括 `enableRTL`, `ariaLabel`, `enableKeyboardNavigation`, `enableHighContrastMode` 等布尔值或字符串配置。`TableViewColumn` 中的 `accessibility.ariaLabel` 和 `cellFormatter` 也应持久化。
  - 当用户在表格工具栏或设置中更改这些配置时，`useTableView` 组合式函数应捕获这些变化，并更新 `TableViewConfig` 对象，然后通过 `updateTableViewConfig` API 接口将其保存到后端。

- **自动保存 vs 手动保存策略**
  - **自动保存**: 对于国际化（`locale`）和可访问性（`enableRTL`, `enableHighContrastMode`）等全局性或偏好设置，可以考虑在用户更改后立即触发自动保存（利用 `scheduleAutoSave` 的防抖机制）。这些设置通常是用户个性化的偏好，即时保存能提供更好的用户体验。
  - **手动保存**: 对于 `TableViewColumn` 内部的 `i18n` 和 `accessibility` 详细配置，由于其可能涉及更复杂的编辑流程（例如在列设置面板中配置），可以将其与列的可见性、顺序等其他配置一同纳入“保存视图”或“另存为视图”的手动保存流程中。这样可以避免频繁的细粒度自动保存，减少后端压力。

### 六、低代码（AMIS）集成方案

- **如何在 AMIS Schema 中支持该能力**
  - **扩展 AMIS Schema**: 需要在 AMIS 的 `table` 或 `columns` 渲染器中扩展其 Schema 定义，增加对国际化和可访问性相关属性的支持。
  - **国际化**: 
    - 为列定义增加 `i18nKey` 属性，例如 `{"type": "text", "name": "name", "label": "${i18nKey:table.column.name}"}`。AMIS 自身支持表达式，可以利用此特性。
    - 为数据格式化增加 `format` 属性，例如 `{"type": "text", "name": "price", "label": "价格", "format": "currency", "formatOptions": {"currency": "CNY"}}`。
    - 整体表格可以增加 `locale` 属性。
  - **可访问性**: 
    - 为表格增加 `accessibility` 属性，例如 `{"type": "table", "source": "/api/data", "accessibility": {"enableRTL": true, "ariaLabel": "用户列表"}}`。
    - 为列定义增加 `ariaLabel` 属性，例如 `{"type": "text", "name": "action", "label": "操作", "accessibility": {"ariaLabel": "操作列"}}`。

- **是否需要注册自定义 AMIS 插件**
  - **需要注册自定义 AMIS 插件**: 鉴于 SecurityPlatform 项目已经有 `business-plugins.ts` 注册了业务插件，并且 AMIS 的 `React 18` 运行时与 Vue 3 的集成需要 `AmisRenderer.vue` 进行包装，因此，为了将 Vue 侧封装的 `useTableI18n` 和 `useTableAccessibility` 能力映射到 AMIS Schema，并确保其在 AMIS 渲染器中正确生效，需要开发一个或多个自定义 AMIS 插件。
  - **插件职责**: 
    - **Schema 解析**: 插件负责解析 AMIS Schema 中新增的国际化和可访问性属性。
    - **Props 转换**: 将解析后的 Schema 属性转换为 `TableView.vue` 组件所需的 Props（例如，将 `i18nKey` 转换为 `label` 的翻译结果，将 `accessibility` 配置转换为 `TableViewProps`）。
    - **渲染器增强**: 可能需要对 AMIS 的 `table` 或 `columns` 渲染器进行增强，以便在渲染时能够注入自定义的 Vue 组件或逻辑，从而实现键盘导航、焦点管理等高级可访问性功能。
    - **运行时集成**: 确保在 `AmisRenderer.vue` 中，AMIS 渲染的表格能够正确接收并应用这些国际化和可访问性配置。

### 七、优先级与实施建议

- **基础版/进阶版/高阶版分级**
  - **基础版 (P1)**: 
    - **国际化**: 确保所有表格列头、工具栏文本、提示信息等均支持 `vue-i18n` 翻译。实现日期、数字、货币的基础本地化格式化，通过 `TableViewConfig.locale` 统一控制。
    - **可访问性**: 确保所有可交互元素（按钮、链接、输入框）具有清晰的 `focus` 态。为表格和列头添加基础的 `aria-label` 属性，提升屏幕阅读器对基本表格结构的理解。
    - **键盘导航**: 实现 Tab 键在表格可交互元素间的顺序导航。
  - **进阶版 (P0)**: 
    - **国际化**: 支持列级别的数据格式化定制（`TableViewColumn.i18n.formatter`）。
    - **可访问性**: 实现完整的键盘导航（Tab 键、方向键在单元格间移动、Enter/Space 激活）。为复杂表格结构（如可排序、可筛选、可编辑单元格）添加更完善的 ARIA 属性。支持 RTL 模式切换。
    - **高对比度**: 提供高对比度模式支持，确保视觉清晰度。
  - **高阶版 (P2)**: 
    - **国际化**: 支持更复杂的自定义数据格式化函数。
    - **可访问性**: 实现高级键盘交互（如快捷键操作）。提供个性化的可访问性设置面板，允许用户自定义字体大小、行高、颜色主题等。
    - **AI 辅助**: 探索利用 AI 自动生成更准确的 ARIA 描述或优化可访问性体验。

- **预估工作量（人天）**
  - **基础版**: 3-5 人天 (P1)
    - 国际化文本梳理与集成：1 人天
    - 基础数据本地化格式化：1 人天
    - 基础 ARIA 属性注入：1 人天
    - Tab 键导航实现：1-2 人天
  - **进阶版**: 7-10 人天 (P0)
    - 列级别数据格式化定制：1-2 人天
    - 完整键盘导航（方向键、激活）：3-4 人天
    - 复杂 ARIA 属性优化：2 人天
    - RTL 模式支持：1-2 人天
    - 高对比度模式：1 人天
  - **高阶版**: 5-8 人天 (P2)
    - 高级键盘交互/个性化设置：3-5 人天
    - 探索 AI 辅助：2-3 人天
  - **总计**: 约 15-23 人天。

- **关键风险与注意事项**
  - **Ant Design Vue 兼容性**: Ant Design Vue 自身的可访问性特性和键盘导航可能与自定义实现冲突，需要仔细测试和协调。特别是其内部对焦点和事件的处理，可能需要通过 `customRow`/`customCell` 或 `v-slot` 进行深度定制。
  - **性能影响**: 复杂的键盘导航和 ARIA 属性注入可能会对大型表格的渲染性能产生一定影响，需要进行性能优化和节流处理。
  - **RTL 样式覆盖**: 实现 RTL 模式时，需要确保全局 CSS 样式和组件库样式能够正确响应，避免布局错乱。可能需要使用 `postcss-rtl` 等工具辅助。
  - **AMIS 集成复杂性**: AMIS Schema 的扩展和自定义插件的开发需要深入理解 AMIS 的渲染机制和生命周期，确保 Vue 组件能够无缝集成并响应 AMIS 的配置变化。
  - **测试覆盖**: 国际化和可访问性功能需要全面的测试覆盖，包括单元测试、集成测试和端到端测试，特别是要进行人工可访问性测试（使用屏幕阅读器、仅键盘操作）。
  - **WCAG 标准**: 确保所有实现符合 Web 内容可访问性指南 (WCAG) 2.1 AA 级别标准，必要时可能需要引入专业的无障碍测试工具和专家进行评估。
  - **持续维护**: 国际化和可访问性是一个持续优化的过程，需要随着业务需求和技术发展不断迭代和完善。

---

## 维度 11：状态持久化能力

- **现状评估**：部分
- **实施优先级**：P0
- **预估人天**：8 天

### 一、现状分析
- **代码库中已有哪些相关实现（文件路径 + 功能说明）**
    - `composables/useTableView.ts` (核心):
        - **列配置管理**（`visible`/`order`/`width`/`pinned`/`align`/`ellipsis`）: 直接支持了记住列宽、列顺序、隐藏列的持久化。
        - **视图持久化**（`saveView`/`saveAs`/`setDefault`/`resetToDefault`）: 提供了保存为个人视图、设为默认视图、重置视图等核心功能。
        - **自动保存**（`scheduleAutoSave`，400ms防抖）: 支持自动保存用户对表格状态的修改，是状态持久化的重要机制。
        - **视图列表搜索**（`searchViews`）: 支持视图的查找和切换，方便用户管理和应用已保存的视图。
    - `components/table/table-view-toolbar.vue` (工具栏):
        - **视图切换下拉**（搜索视图）: 提供用户界面入口，用于选择和应用不同的表格视图。
        - **保存/另存为/设为默认/重置**功能: 提供用户界面入口，用于管理表格视图的生命周期。
        - **列设置面板**（显示/隐藏/上移/下移/固定左右）: 提供用户界面，用于调整列的可见性、顺序和宽度，并触发状态更新。
    - `types/api.ts` (核心类型):
        - `TableViewColumnConfig`: 包含 `key`/`visible`/`order`/`width`/`pinned`/`align`/`ellipsis` 等字段，这些字段直接对应列的持久化状态。
        - `TableViewConfig`: 包含 `columns`/`density`/`pagination`/`sort`/`filters`/`groupBy`/`aggregations`/`queryPanel`/`queryModel`/`mergeCells`。其中 `pagination` (分页大小)、`sort` (排序条件)、`filters` (筛选条件) 已经有类型定义，表明后端支持这些状态的持久化。
    - `services/api-system.ts` (表格视图API):
        - 提供了完整的TableView CRUD API，包括查询、创建、更新、设置默认、删除等，这些API是视图持久化的核心支撑，确保表格状态能在后端进行存储和检索。

- **存在哪些缺口和不足**
    - **排序筛选条件、分页大小的UI集成与持久化**: 尽管 `TableViewConfig` 中有 `sort`、`filters` 和 `pagination` 的类型定义，但代码摘要中未明确指出前端 `useTableView` 或相关组件是否已完全集成这些状态的UI交互，并确保其能被 `scheduleAutoSave` 捕获并持久化。
    - **分享视图链接**: 目前代码库中没有直接支持生成可分享的视图链接的功能。这需要将当前表格的完整状态编码到URL中，或通过后端服务生成一个短链接。
    - **URL参数同步表格状态**: 缺乏从URL参数解析表格状态并应用到表格的功能，以及在表格状态变化时更新URL参数的机制。
    - **保存为个人视图**: 已有基本功能，但可能需要更细致的权限控制和用户体验优化，例如视图的命名、描述等。
    - **自动保存覆盖范围**: 需要确保 `scheduleAutoSave` 能够全面覆盖所有需要持久化的状态，包括列配置、排序、筛选、分页等。

### 二、封装目标
- **该维度需要封装哪些核心能力（对照用户需求清单逐条说明）**
    - **记住列宽**: 用户调整列宽后，其宽度信息（`TableViewColumnConfig.width`）应能被捕获并自动或手动持久化到 `TableViewConfig` 中。
    - **记住列顺序**: 用户拖拽调整列顺序后，其顺序信息（`TableViewColumnConfig.order`）应能被捕获并自动或手动持久化到 `TableViewConfig` 中。
    - **隐藏列**: 用户通过列设置面板隐藏/显示列后，其可见性信息（`TableViewColumnConfig.visible`）应能被捕获并自动或手动持久化到 `TableViewConfig` 中。
    - **排序筛选条件**: 用户进行排序和筛选操作后，其排序（`TableViewConfig.sort`）和筛选（`TableViewConfig.filters`）条件应能被捕获并自动或手动持久化到 `TableViewConfig` 中。
    - **分页大小**: 用户调整分页大小后，其分页配置（`TableViewConfig.pagination`）应能被捕获并自动或手动持久化到 `TableViewConfig` 中。
    - **保存为个人视图**: 提供明确的用户操作入口，允许用户将当前表格的完整状态（包括上述所有配置）保存为一个命名视图，并支持设为默认视图、更新、删除等操作。
    - **分享视图链接**: 提供功能，将当前表格的完整状态编码为可分享的URL。当其他用户访问该URL时，表格应能自动恢复到分享时的状态。
    - **URL参数同步表格状态**: 实现双向同步：
        1. 表格状态变化时，自动更新浏览器URL中的相关参数。
        2. 页面加载时，解析URL参数，并将其应用到表格状态中，实现表格状态的恢复。

- **普通面板场景 vs 低代码场景的差异处理**
    - **普通面板场景**: 主要通过 `useTableView` Composable 和 `table-view-toolbar.vue` 组件提供能力。封装方案应侧重于 Vue 组件和 Composable 的设计，确保状态的响应式和与Ant Design Vue表格的集成。状态持久化直接通过 `api-system.ts` 提供的 API 进行。
    - **低代码场景 (AMIS)**: AMIS 表格的状态持久化需要通过 AMIS Schema 来描述和控制。这意味着需要将上述表格状态（列配置、排序、筛选、分页等）映射到 AMIS 的 `table` 组件或自定义组件的属性中。分享视图链接和URL参数同步可能需要通过 AMIS 的 `page` 或 `crud` 组件的 `data` 属性或自定义 `actions` 来实现。可能需要开发自定义 AMIS 插件来扩展其能力，以便更好地与 `TableViewConfig` 对接。

### 三、核心数据模型 / 类型定义
现有 `types/api.ts` 中的 `TableViewConfig` 和 `TableViewColumnConfig` 已经为状态持久化提供了良好的基础。我们主要需要完善 `TableViewConfig` 中 `sort`、`filters`、`pagination` 的具体结构，并考虑为URL参数同步增加相关类型。

- **给出 TypeScript 接口/类型定义（代码块）**
```typescript
// types/api.ts (完善或新增)

interface TableViewPaginationConfig {
  current: number; // 当前页码
  pageSize: number; // 每页大小
  total?: number; // 总条数，可选，前端可根据需要忽略或从后端获取
  showSizeChanger?: boolean; // 是否显示每页大小切换器
  showQuickJumper?: boolean; // 是否显示快速跳转
}

interface TableViewSortConfig {
  field: string; // 排序字段
  order: 'ascend' | 'descend'; // 排序顺序
}

interface TableViewFilterCondition {
  field: string; // 筛选字段
  operator: string; // 运算符，例如 'eq', 'like', 'gt', 'lt', 'in'
  value: any; // 筛选值
}

interface TableViewFilterGroup {
  logic: 'AND' | 'OR'; // 逻辑关系
  conditions?: TableViewFilterCondition[];
  groups?: TableViewFilterGroup[]; // 支持嵌套筛选
}

// 扩展 TableViewConfig 以包含更详细的URL参数同步配置
interface TableViewConfig {
  // ... 现有字段
  columns: TableViewColumnConfig[];
  density: TableViewDensity; // 'compact' | 'default' | 'comfortable'
  pagination?: TableViewPaginationConfig; // 完善分页配置
  sort?: TableViewSortConfig[]; // 支持多列排序
  filters?: TableViewFilterGroup; // 完善筛选配置
  // 新增用于URL参数同步的配置，可选
  urlSync?: {
    enabled: boolean; // 是否启用URL参数同步
    paramPrefix?: string; // URL参数前缀，避免冲突，例如 'tv_'
    debounceTime?: number; // URL更新防抖时间，默认为300ms
  };
}

// 新增用于分享视图的类型
interface ShareableViewLink {
  viewId?: string; // 如果是已保存视图，则为视图ID
  encodedState?: string; // 如果是临时视图，则为编码后的表格状态
  expiresAt?: string; // 链接过期时间，可选
}
```

- **说明与现有 TableViewConfig 的集成方式**
    - `TableViewConfig` 已经包含了 `columns`、`density`、`pagination`、`sort`、`filters` 等字段，只需按照上述定义完善 `pagination`、`sort` 和 `filters` 的具体结构即可。这些结构将直接映射到表格的UI状态和后端API的请求参数。
    - 新增的 `urlSync` 字段将作为 `TableViewConfig` 的一个可选属性，用于控制URL参数同步的行为。这使得每个视图都可以独立配置是否启用URL同步以及同步的细节。
    - `ShareableViewLink` 是一个独立于 `TableViewConfig` 的类型，用于表示分享链接的结构，它可能包含视图ID或编码后的表格状态。

### 四、组件/Composable 封装方案
- **新增或改造哪些文件（给出文件路径建议）**
    - **改造 `composables/useTableView.ts`**: 这是核心改造点，需要增强其状态管理和同步逻辑。
        - 增加对 `TableViewConfig.pagination`、`TableViewConfig.sort`、`TableViewConfig.filters` 的响应式管理。
        - 实现 `urlSync` 逻辑：监听表格状态变化，防抖更新URL参数；在组件挂载时，解析URL参数并应用到表格状态。
        - 增加生成分享链接的方法。
    - **改造 `components/table/table-view-toolbar.vue`**: 
        - 增加排序和筛选条件的UI入口（如果Ant Design Vue表格本身未提供，或需要更高级的筛选器）。
        - 增加“分享视图”按钮，点击后弹出分享链接。
        - 确保分页大小的调整能够触发 `useTableView` 中的状态更新。
    - **新增 `utils/url-state-encoder.ts`**: 用于表格状态的编码和解码，支持将 `TableViewConfig` 对象序列化为URL安全字符串，并反序列化回来。
    - **改造 `composables/useCrudPage.ts`**: 如果 `useCrudPage` 负责处理分页、排序、筛选的请求参数，需要确保它能与 `useTableView` 协同工作，从 `useTableView` 获取当前状态，并将其传递给后端API。

- **关键 Props/Emits/Expose 设计（代码块）**
    - **`useTableView.ts` (Composable)**
        - **Expose (返回)**:
        ```typescript
        interface UseTableViewReturn {
          // ... 现有返回项
          currentTableViewConfig: Ref<TableViewConfig>; // 当前激活的表格视图配置，响应式
          updatePagination: (pagination: TableViewPaginationConfig) => void; // 更新分页状态
          updateSort: (sort: TableViewSortConfig[]) => void; // 更新排序状态
          updateFilters: (filters: TableViewFilterGroup) => void; // 更新筛选状态
          generateShareLink: () => Promise<string>; // 生成分享链接
          applyUrlParams: () => void; // 从URL参数应用表格状态
          syncStateToUrl: (config: TableViewConfig) => void; // 同步表格状态到URL
        }
        ```
    - **`components/table/table-view-toolbar.vue` (组件)**
        - **Props**:
        ```typescript
        interface TableViewToolbarProps {
          tableKey: string; // 表格唯一标识
          currentViewId?: string; // 当前激活视图ID
          currentTableViewConfig: TableViewConfig; // 当前表格配置
          // ... 其他现有Props
        }
        ```
        - **Emits**:
        ```typescript
        interface TableViewToolbarEmits {
          (e: 'update:currentViewId', viewId: string): void;
          (e: 'saveView', config: TableViewConfig): void;
          (e: 'saveAsView', config: TableViewConfig, name: string): void;
          (e: 'setDefaultView', viewId: string): void;
          (e: 'resetView'): void;
          // ... 其他现有Emits
        }
        ```

- **核心实现逻辑说明**
    1. **状态响应式化**: 在 `useTableView` 中，确保 `TableViewConfig` 的所有可持久化部分（包括 `pagination`、`sort`、`filters`）都是响应式的，以便UI操作能直接修改这些状态。
    2. **自动保存增强**: `scheduleAutoSave` 需要监听 `currentTableViewConfig` 的深层变化，并在防抖后调用 `updateTableViewConfig` API。
    3. **URL参数同步**: 
        - **状态到URL**: 创建一个 `watch` 监听 `currentTableViewConfig` 的变化（当 `urlSync.enabled` 为 `true` 时），使用 `utils/url-state-encoder.ts` 将 `currentTableViewConfig` 编码为URL参数，并通过 `history.replaceState` 更新URL。需要进行防抖处理以避免频繁更新。
        - **URL到状态**: 在 `useTableView` 初始化时，解析当前URL中的参数，如果存在表格状态相关的参数，则使用 `utils/url-state-encoder.ts` 解码并应用到 `currentTableViewConfig` 中。
    4. **分享链接生成**: `generateShareLink` 方法将当前 `currentTableViewConfig` 编码，并结合当前页面URL生成一个完整的分享链接。可以考虑将编码后的状态上传到后端，由后端返回一个短链接，以避免URL过长。
    5. **Ant Design Vue 表格集成**: `a-table` 组件的 `columns`、`pagination`、`sorter`、`filter` 等属性应绑定到 `useTableView` 提供的响应式状态。当 `a-table` 触发 `change` 事件时，需要将新的分页、排序、筛选状态同步回 `useTableView`。

### 五、与持久化视图的集成
- **该维度的状态如何持久化到 TableViewConfig**
    - **列宽/列顺序/隐藏列**: 这些状态已经通过 `TableViewColumnConfig` 包含在 `TableViewConfig.columns` 数组中，`useTableView` 已经处理了这部分的持久化。
    - **排序筛选条件/分页大小**: 当用户在表格UI上进行排序、筛选或改变分页大小时，`a-table` 会触发相应的事件。在事件处理函数中，`useTableView` 需要捕获这些变化，并更新其内部的 `currentTableViewConfig` 响应式对象中的 `pagination`、`sort` 和 `filters` 字段。然后，通过 `scheduleAutoSave` 或用户手动点击“保存”按钮，将更新后的 `currentTableViewConfig` 提交给 `api-system.ts` 的 `updateTableViewConfig` API，从而实现持久化。

- **自动保存 vs 手动保存策略**
    - **自动保存 (scheduleAutoSave)**: 适用于用户对表格进行临时性调整（如调整列宽、隐藏列、简单排序）的场景。`useTableView` 中的 `scheduleAutoSave` 机制应监听 `currentTableViewConfig` 的变化，并在用户停止操作一段时间后（例如400ms防抖）自动将当前状态保存到后端。这提供了流畅的用户体验，避免用户频繁手动保存。
    - **手动保存 (saveView/saveAs)**: 适用于用户希望明确保存一个特定视图，或者将当前状态另存为新视图的场景。这通常通过 `table-view-toolbar.vue` 中的“保存”、“另存为”按钮触发。手动保存允许用户为视图命名、添加描述，并选择是否设为默认视图。对于复杂的筛选条件或重要的视图配置，手动保存是更合适的策略。
    - **策略建议**: 两种策略应并存。自动保存作为背景机制提升用户体验，手动保存作为显式操作提供视图管理能力。`scheduleAutoSave` 应该只更新当前激活视图的配置，而“另存为”则创建新的视图记录。

### 六、低代码（AMIS）集成方案
- **如何在 AMIS Schema 中支持该能力**
    - **列配置 (列宽/顺序/隐藏)**: AMIS 的 `table` 组件支持 `columns` 属性，其中每个列可以定义 `width`、`fixed` 等。对于列的可见性和顺序，AMIS 提供了 `columnsTogglable` 和 `columnsDraggable` 属性，允许用户在运行时调整。这些运行时调整的状态需要通过 AMIS 的 `onEvent` 或 `data` 绑定机制捕获，并映射到 `TableViewConfig` 结构。
    - **排序筛选条件/分页大小**: AMIS 的 `crud` 或 `table` 组件通常自带分页、排序和筛选功能。其状态可以通过 `data` 属性或 `onEvent` 钩子获取。例如，`crud` 组件的 `onQuery` 事件会返回当前的 `page`、`perPage`、`orderBy`、`orderDir`、`filter` 等参数。我们需要将这些参数转换为 `TableViewConfig` 中的 `pagination`、`sort`、`filters` 结构。
    - **保存为个人视图/分享视图链接/URL参数同步**: 
        - **视图管理**: 可以在 AMIS Schema 中嵌入一个自定义的 Vue 组件（通过 `AmisRenderer.vue` 包装），该组件内部使用 `useTableView` 和 `table-view-toolbar.vue` 来提供视图管理功能。或者，通过 AMIS 的 `action` 按钮触发自定义 API 调用，实现视图的保存和加载。
        - **URL参数同步**: AMIS 的 `crud` 组件支持 `syncLocation` 属性，可以自动将查询参数同步到URL。但其同步的参数格式可能与 `TableViewConfig` 不完全匹配。需要通过自定义 `data` 映射或 `onQuery` 钩子进行转换。

- **是否需要注册自定义 AMIS 插件**
    - **强烈建议注册自定义 AMIS 插件**: 考虑到 `SecurityPlatform` 项目中 `TableViewConfig` 的复杂性和统一性要求，以及与后端API的紧密集成，开发一个自定义 AMIS 插件是最佳实践。
    - **插件功能**: 
        - **自定义表格组件**: 封装 `a-table` 和 `useTableView` 的能力，使其能直接在 AMIS Schema 中以 `<amis-table-view>` 这样的形式使用。
        - **状态映射**: 插件内部负责将 AMIS Schema 中的属性和事件与 `TableViewConfig` 进行双向映射和转换。
        - **视图管理集成**: 插件可以提供一个内置的视图管理UI，或者暴露接口让 AMIS Schema 可以配置视图管理功能。
        - **URL同步适配**: 插件可以处理 AMIS 的URL同步逻辑，使其与 `TableViewConfig.urlSync` 配置保持一致。
    - **注册方式**: 在 `business-plugins.ts` 中注册该自定义插件，使其能在 AMIS 编辑器和运行时中使用。

### 七、优先级与实施建议
- **基础版/进阶版/高阶版分级**
    - **基础版 (P1)**:
        - 完善 `useTableView` 对排序、筛选、分页大小的响应式管理和自动保存。
        - 确保 `table-view-toolbar.vue` 中的列设置、视图保存/切换功能能全面覆盖所有可持久化状态。
        - 实现基础的URL参数同步（状态到URL，URL到状态），但可能只支持简单的键值对，不包含复杂嵌套结构。
        - 预估工作量：5人天。
    - **进阶版 (P0)**:
        - 在基础版之上，实现 `TableViewConfig` 中 `filters` 复杂嵌套结构的UI交互和持久化。
        - 实现完整的分享视图链接功能，包括状态编码、链接生成、以及通过链接恢复表格状态。
        - 优化URL参数同步，支持更复杂的 `TableViewConfig` 结构编码和解码。
        - 预估工作量：8人天。
    - **高阶版 (P2)**:
        - 在进阶版之上，开发自定义 AMIS 插件，将 `useTableView` 的所有能力无缝集成到 AMIS 低代码表格中。
        - 插件应提供 AMIS Schema 配置项，允许在低代码平台中灵活配置表格的持久化行为和视图管理。
        - 预估工作量：10人天。

- **预估工作量（人天）**
    - 综合考虑，实现该维度完整能力（达到进阶版水平，并为高阶版打下基础）的预估人天数为 **8人天**。

- **关键风险与注意事项**
    - **状态同步复杂性**: 表格状态涉及多个方面（列、分页、排序、筛选），确保这些状态在UI、`useTableView`、`TableViewConfig` 和URL参数之间保持一致和同步，是最大的挑战。需要仔细设计状态流和更新机制，避免竞态条件和数据不一致。
    - **URL参数长度限制**: 如果表格状态非常复杂，编码后的URL参数可能会过长，超出浏览器或服务器的限制。需要考虑状态压缩、使用短链接服务或仅在URL中传递视图ID的策略。
    - **AMIS 集成复杂度**: AMIS 的扩展性虽然强，但将其内部状态与 `TableViewConfig` 进行双向映射和同步，需要深入理解 AMIS 的事件机制和数据流。自定义插件的开发和维护成本较高。
    - **性能问题**: 频繁的URL更新或状态编码/解码可能会影响页面性能。需要合理使用防抖和节流，并优化编码/解码算法。
    - **安全性**: 分享视图链接时，需要考虑数据权限和敏感信息泄露的风险。确保分享的视图不会暴露用户无权访问的数据或操作。
    - **用户体验**: 视图管理功能需要清晰直观的UI，避免用户混淆自动保存和手动保存的区别。分享链接的生成和使用流程也应尽可能简单。



---

## 维度 12：业务高级能力

- **现状评估**：缺失
- **实施优先级**：P2
- **预估人天**：20 天

### 一、现状分析

- **代码库中已有哪些相关实现（文件路径 + 功能说明）**
    - 从 `codebase_summary.md` 来看，当前代码库中直接支持维度12“业务高级能力”的实现非常有限。
    - `types/api.ts` 中的 `TableViewConfig` 类型定义包含 `groupBy` 和 `aggregations` 字段，这表明后端API层面可能已经为行分组聚合提供了数据模型支持，但前端UI层面尚未实现。
        ```typescript
        // types/api.ts
        interface TableViewConfig {
          // ... 其他配置
          groupBy?: TableViewGroupBy[];
          aggregations?: TableViewAggregation[];
          mergeCells?: MergeCellRule[]; // 合并单元格，与部分高级表格（如交叉表）相关
          // ...
        }

        interface TableViewGroupBy {
          field: string;
          // ... 其他分组配置
        }

        interface TableViewAggregation {
          field: string;
          func: 'sum' | 'avg' | 'count' | 'max' | 'min';
          // ... 其他聚合配置
        }
        ```
    - `composables/useTableView.ts` 中提到了 `computeMergeSpans/getMergeCells`，这与合并单元格功能相关，而合并单元格是交叉表、透视表等高级表格的基础能力之一。
    - `types/dynamic-tables.ts` 定义了 `DynamicColumnDef`，其中包含 `name/label/type` 等，这些是动态列的基础，但未涉及计算列的定义。

- **存在哪些缺口和不足**
    - **树表主子表**: `codebase_summary.md` 的“现有能力缺口”明确指出“树形表格 - 未封装”。目前 Ant Design Vue 的 `a-table` 组件支持树形数据展示，但缺乏封装层面的统一管理和配置。
    - **交叉表/透视表/多维分析**: 完全缺失。这些功能需要复杂的数据转换、聚合逻辑和交互式配置界面。
    - **行分组聚合**: 虽然 `TableViewConfig` 中有 `groupBy` 和 `aggregations` 的类型定义，但前端没有对应的UI组件和逻辑来支持用户配置和展示分组聚合结果。
    - **看板式表格/甘特表/日历表格**: 完全缺失。这些是特定业务场景下的表格展现形式，通常需要独立的组件实现。
    - **Excel风格冻结窗格**: 缺失。Ant Design Vue 的 `a-table` 支持 `fixed` 属性固定列，但缺乏 Excel 风格的冻结行和拖拽调整冻结线的能力。
    - **可配置计算列**: 缺失。需要支持用户定义计算公式，并实时计算展示结果。
    - **条件格式规则引擎**: 缺失。需要一套规则定义和管理机制，根据数据值动态改变单元格或行的样式。
    - **低代码集成**: 对于上述高级能力，AMIS 层面也缺乏直接的 Schema 支持和可视化配置能力。

### 二、封装目标

- **该维度需要封装哪些核心能力（对照用户需求清单逐条说明）**
    - **树表主子表**: 
        - **树形表格**: 支持层级数据的展示，包括展开/收起、懒加载子节点、自定义层级缩进和图标。
        - **主子表**: 支持在表格行内展开子表格，展示与主行关联的详细数据。
    - **交叉表/透视表**: 
        - 支持将行数据转换为列数据，进行多维度的数据聚合和展示。
        - 提供配置界面，允许用户拖拽字段到行、列、值区域，并选择聚合函数。
    - **行分组聚合**: 
        - 支持按一个或多个字段对数据进行分组，并展示分组汇总信息。
        - 提供分组行的展开/收起功能，支持自定义分组头渲染。
    - **多维分析**: 
        - 在交叉表/透视表的基础上，支持钻取（Drill-down）、切片（Slice）、旋转（Pivot）等操作，进行更深层次的数据探索。
    - **看板式表格/甘特表/日历表格**: 
        - 封装特定业务场景下的表格组件，如项目管理中的甘特图、日程安排中的日历视图、任务管理中的看板视图。
    - **Excel风格冻结窗格**: 
        - 支持固定表格的顶部行和左侧列，实现类似 Excel 的冻结窗格效果。
        - 考虑支持用户拖拽调整冻结线。
    - **可配置计算列**: 
        - 允许用户通过表达式或公式定义新的计算列，支持引用现有列数据进行计算。
        - 提供公式编辑器，支持公式校验和实时预览。
    - **条件格式规则引擎**: 
        - 提供一套规则配置界面，允许用户定义基于数据条件的格式化规则（如背景色、字体色、图标等）。
        - 支持多条规则的优先级管理和条件组合（AND/OR）。

- **普通面板场景 vs 低代码场景的差异处理**
    - **普通面板场景**: 
        - 通过 Vue 组件的 Props 传入配置对象，直接渲染高级表格功能。
        - 配置界面（如透视表配置器、条件格式规则编辑器）作为独立的 Vue 组件提供，供业务页面集成。
    - **低代码场景 (AMIS)**: 
        - 通过扩展 AMIS 的 Schema 定义，将高级表格功能的配置项集成到 AMIS 的 `table` 组件中。
        - 对于复杂交互或特定展现形式（如甘特图），可能需要开发自定义 AMIS 组件来封装 Vue 组件。
        - 需要提供 AMIS 编辑器中的可视化配置能力，让用户能够通过拖拽、表单填写等方式配置这些高级功能。

### 三、核心数据模型 / 类型定义

为了支持维度12的业务高级能力，需要在 `types/api.ts` 和 `types/table-advanced.ts` (新增) 中扩展 `TableViewConfig` 及相关类型定义。

- **给出 TypeScript 接口/类型定义（代码块）**

    ```typescript
    // types/table-advanced.ts (新增文件)

    /**
     * 树形表格配置
     */
    export interface TreeTableConfig {
      isTree?: boolean; // 是否启用树形表格
      treeNodeKey?: string; // 树节点唯一标识的字段名，默认为 'id'
      parentKey?: string; // 父节点标识的字段名，默认为 'parentId'
      childrenKey?: string; // 子节点列表的字段名，默认为 'children'
      lazyLoad?: boolean; // 是否支持懒加载子节点
      lazyLoadApi?: string; // 懒加载子节点的API接口
      indentSize?: number; // 树节点缩进像素，默认为 16
    }

    /**
     * 主子表配置 (行内展开子表格)
     */
    export interface MasterDetailTableConfig {
      enable?: boolean; // 是否启用主子表
      detailComponent?: string; // 子表格渲染的组件名称或路径
      detailComponentProps?: Record<string, any>; // 传递给子组件的额外属性
      getDetailData?: (record: any) => Promise<any[]>; // 获取子表格数据的方法
    }

    /**
     * 交叉表/透视表配置
     */
    export interface PivotTableConfig {
      enable?: boolean; // 是否启用透视表
      rows: string[]; // 行维度字段
      columns: string[]; // 列维度字段
      values: Array<{ field: string; aggregate: 'sum' | 'avg' | 'count' | 'max' | 'min' }>; // 值字段及聚合方式
      rowGrandTotal?: boolean; // 是否显示行总计
      colGrandTotal?: boolean; // 是否显示列总计
      // ... 更多透视表特有配置
    }

    /**
     * 行分组聚合配置 (扩展 TableViewGroupBy 和 TableViewAggregation)
     */
    export interface RowGroupConfig {
      enable?: boolean; // 是否启用行分组
      groupBy?: TableViewGroupBy[]; // 分组字段，可多级
      aggregations?: TableViewAggregation[]; // 聚合配置
      showGroupSummary?: boolean; // 是否显示分组汇总行
      expandAllGroups?: boolean; // 默认是否展开所有分组
      groupHeaderRender?: string; // 自定义分组头渲染函数或组件名
    }

    /**
     * Excel风格冻结窗格配置
     */
    export interface FreezePaneConfig {
      enable?: boolean; // 是否启用冻结窗格
      freezeRows?: number; // 冻结顶部行数
      freezeCols?: number; // 冻结左侧列数
      // TODO: 考虑拖拽调整冻结线功能
    }

    /**
     * 可配置计算列定义
     */
    export interface CalculatedColumn {
      key: string; // 计算列的唯一标识
      title: string; // 列头显示名称
      expression: string; // 计算表达式，例如 'price * quantity' 或 'concat(firstName, 

---

