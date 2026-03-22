# 平台下一代核心架构演进：7 大方向实施计划 (Plan)

此文档为基于 `SecurityPlatform_NextGen_Architecture_MERGED.md` 提取的 7 大核心方向的任务实施计划。遵循小步慢跑、文档驱动与等保2.0合规原则，将需求拆分为前端与后端任务，按优先级（Phase 1 / Phase 2 / Phase 3）进行落地。每次实施具体的用例将拆分对应的 `prd-case-*.md` 文件。

## 一、 Phase 1: 核心基础构建 (P0)

本阶段主要完成 7 大方向中最核心的 UI 骨架和后端基础设施，确保每个方向的最小可用版本 (MVP) 跑通。

### 1. 高级查询面板 (AdvancedQueryPanel)
- **前端:**
  - [NEW] 开发 `QueryConditionBuilder.vue` 核心条件构建器（支持字段、操作符、值的选择）。
  - [NEW] 构建 `AdvancedQueryPanel.vue` 组件基础框架与 UI（集成条件构建器与实时预览）。
  - [NEW] 引入 `AdvancedQueryCondition` 和 `QueryOperator` 的类型定义。
- **后端:**
  - [MODIFY] 扩展 `DynamicRecordQueryRequest` 和 `services/dynamic-tables.ts`，支持嵌套查询条件的解析与动态 SQL 构建（防注入）。

### 2. 可视化实体设计器 (ERD Canvas)
- **前端:**
  - [NEW] 引入并集成 AntV X6 (或同等库) 绘制 ERD Canvas 基础框架。
  - [NEW] 实现实体、字段节点的拖拽式添加与基础编辑。
  - [NEW] 实现实体关系（1:N）的连线与可视化配置记录。
- **后端:**
  - [MODIFY] 扩充 `DynamicTable` 相关元数据结构，支撑实体关系的持久化及 CRUD。

### 3. 低代码表单变量绑定引擎
- **前端:**
  - [NEW] 实现轻量级变量作用域管理机制（组件级/页面级）。
  - [NEW] 在 `FormDesignerPage.vue` 中提供变量类型推断机制。
  - [NEW] 实现 JavaScript 表达式引擎的基础功能，支持 Mustache `{{}}` 数据绑定解析。
  - [NEW] 添加 AMIS Schema 预处理器，将引擎自定义的变量配置转为原生属性。
- **后端:**
  - [NEW] 提供后端配套的规则执行与统一校验 API，确保前后端计算逻辑与校验一致。

### 4. 数据库连接器高级预览与管理
- **前端:**
  - [MODIFY] 扩展现有的 `TenantDataSourcesPage` 加入“高级管理”页面入口及多数据源切换支持。
  - [NEW] 构建 `AdvancedDataPreviewPage` 容器页面。
  - [NEW] 开发内嵌的 SQL 编辑器 `SqlEditor.vue`（集成 Monaco Editor/CodeMirror）。
  - [NEW] 实现数据预览的表格展示界面 `DataPreviewTable.vue`。
- **后端:**
  - [NEW] 创建 `SQLQueryService` 提供带有防注入及基于等保2.0角色授权鉴权的查询接口。

### 5. 查询+Grid 一体化视图 (QueryGrid Unified View)
- **前端:**
  - [NEW] 构建顶层协同容器组件 `QueryGridUnifiedView.vue`，实现表单查询面板与展示表格的联动。
  - [MODIFY] 升级增强基础 `ProTable.vue`，接收查询条件用于渲染。
  - [MODIFY] 扩展 `useCrudPage.ts` 以读取并传递 `AdvancedQueryPanel` 中的状态。
  - [MODIFY] 扩展 `TableViewConfig` 及类型，以确保查询面板配置能够持久化报错。

### 6. 多设备预览与响应式设计
- **前端:**
  - [NEW] 实现 `MultiDevicePreviewWrapper.vue`。
  - [NEW] 通过 iframe 沙箱实现单视口模拟的 `DeviceFrame.vue` 引擎。
  - [NEW] 设计用于分辨率调整及设备选择的 `DeviceToolbar.vue` 控件。
  - [MODIFY] 将 `FormDesignerPage.vue` 的原装 AMIS 简单移动端预览能力替换为此多设备引擎。

### 7. 平台架构与业务场景加速 (基建层)
- **后端:**
  - [NEW] 构建严格遵循多租户隔离与更细粒度 RBAC 的资源拦截鉴权层。
  - [NEW] 对这 7 个方向新增的修改及特权操作接入安全审计追踪记录，强制合规。


---

## 二、 Phase 2: 深度集成与功能增强 (P1)

深化核心功能，提高跨组件的联动与智能性，重点关注等保数据安全建设。

### 1. 高级查询面板 
- **前端:** 完善变量绑定 UI，支持运行时的环境上下文关联查询；支持实体字段直接拖拽进入查询面板关联构建。
- **后端:** `TableViewConfig` 高级查询复杂结构的长期云端持久化。

### 2. 可视化实体设计器 
- **前端:** 增加数据库视图（View）的可视化 JOIN 设计；补充唯一约束、数据库索引、外键配置 UI 面板。
- **后端:** 提供视图的元数据模型转换，实装同步实体约束到物理数据库底层的能力并支持查询预览。

### 3. 低代码表单变量绑定引擎
- **前端:** 集成 AMIS 高级前后端校验规则与定制格式化器属性面板；支持实体拖拽至面板自动生成相映射的 AMIS 表单项。
- **后端:** 审计及日志埋点支持对所有通过低代码引擎的修改进行全程留痕。

### 4. 数据库连接器高级预览与管理
- **前端:** 提供完整操作引导的操作面板（包含清除、备份、回复配置）。
- **后端:** 在 `DataOperationService` 中基于租户权限实现整表备份、数据恢复和软硬清理能力。

### 5. 查询+Grid 一体化视图
- **前端:** 为新视图开发 `atlas-pro-table` 原生 AMIS 插件进行系统级注册。新增 `ViewModeSwitcher.vue` 和专属的多卡片流界面（`CardView.vue`）。

### 6. 多设备预览与响应式设计
- **前端:** 改造 AMIS 编辑器属性面板，支持按断点(breakpoints)的栅格属性分配；支持在 iframe 中设置假拟 User-Agent 与不同 DPR。实现用于预览实时注入数据的模拟器（Mock Data Generator）。


---

## 三、 Phase 3: 高级特性、转换引擎与复杂场景 (P2)

解决最为复杂的底层特定业务场景应用，系统级进阶。

### 1. 高级查询面板
- **后端:** 实现后端智能查询提示与性能风险阻断（判断缺失索引或过度全表扫描）。
  - 脱敏策略及强权限挂钩策略：对于未授权用户，返回自动打码或脱敏数值。
  
### 2. 可视化实体设计器
- **前端:** 处理 1:1, M:N 多重层级关系的关联表图形生成。
- **前后端:** 数据转换（ETL）底层逻辑对接，可视化构建映射管道。

### 3. 数据库连接器高级预览与管理
- **前端:** 数据迁移的向导式可视化流程控制 (`DataMigrationTool.vue`)。
- **后端:** 实现跨越租户或数据源的表和字段迁移底层执行协程服务引擎。

### 4. 查询+Grid 一体化视图
- **前端:** 增加 `KanbanView.vue` 和 `GanttView.vue` 两种高级排期与甘特图视图。
- **前后端:** 加入更复杂的批量任务流操作控制集成。

### 5. 多设备预览与响应式设计
- **前端:** 在画布开发时嵌入预检测组件边界相交或文字溢出机制（静态冲突排查）。
- **前端:** 编写 Linter 防止设计师生成不支持响应式的冲突型 AMIS json 配置。

### 6. 平台架构与业务场景加速 (高可用层)
- **后端运维:** 集成消息中间件对长耗时生成操作解耦，增加 API 容错熔断管理。

---

## 质量保障与实施规范 
1. **测试驱动联调**：每一开发任务均需新增或更新对应的 `.http` 测试用例和涉及前端的 Playwright (E2E) 测试用例，确保在 `http://localhost:5000`与`http://localhost:5173`本地启动完全通过测试方可提交。
2. **前后端接口先行与规范契约**：首先向 `docs/contracts.md` 或新定义的 `api` 文件添加变更接口后才能投入编码。后端 DTO 完全强类型与验证，禁止 `any` 参数。
3. **等保 2.0 强制控制点**：一切新增写操作（例如表单提交引擎、持久化视图、数据结构维护等 API）必须要求传入 `Idempotency-Key` 及前端拦截验证反 CSRF（`X-CSRF-TOKEN`）。使用严格的字典集合或配置降低接口权限粒度。
