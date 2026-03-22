# PRD-Case: 可视化实体设计器 (ERD Canvas) - P0 阶段

## 1. 业务目标
平台现有的实体建模仅限于简单的表格展示（`DynamicTableDetail`），无法直观表达多表之间的关联关系（如 `1:N`，`N:M`）。
我们需要引入可视化的 ERD (Entity-Relationship Diagram) 画布，支持拖拽创建新实体、连线建立主外键/逻辑关系，为复杂业务场景打下模型基础。

## 2. 需求拆分 (前后端计划)

### 2.1 后端实现计划
1. **核对与完善 `DynamicRelationDefinition`**：
   - 当前后端 DTO 模型中已存在 `DynamicRelationDefinition`，需要确认 `DynamicTableServices` 是否已经完整支持对该关系列表的持久化（GET/PUT）。
   - 提供一个聚合 API（如果尚未存在），可以一次性获取当前租户下的所有子实体（或部分模块相关联的实体）及其关联关系，方便前端画布一次性渲染。

### 2.2 前端实现计划
1. **依赖引入**：
   - 必须通过 `npm install @antv/x6 @antv/x6-plugin-dnd @antv/x6-plugin-selection @antv/x6-plugin-keyboard @antv/x6-vue-shape -S` 引入蚂蚁金服的 X6 绘图引擎作为底层核心。
2. **画布组件开发 (`ERDCanvas.vue`)**：
   - 全屏/自适应区域初始化的 X6 `Graph` 画布。
   - 自定义 X6 Node，使用 `@antv/x6-vue-shape` 将实体及其拥有的字段列表（主键高亮，数据类型标签）渲染为一个个卡片。
   - 左侧边栏/顶部栏提供 `Dnd` (Drag and Drop) 拖拽能力，能够从侧边栏把新实体拖入画布。
3. **关系连线操作**：
   - 允许用户从源实体字段拖出连线，连接到目标实体的属性字段端点。
   - 连线建立后，弹出配置面板（`RelationConfigModal.vue`），配置关联基数（`1:1`, `1:N`）与级联规则（CascadeRule）。
4. **与服务端的全量/增量同步**：
   - 支持一键 "保存画布"，将所有图节点坐标数据（前端专有字段，如 `x`, `y` 坐标存储）与实体结构数据进行打包上传（后端可能需要提供一个统一拓扑存储能力，或只保存语义关系和扩展属性）。

## 3. 测试与验证标准
1. 进入 `EntityModeling` 环境，能够打开可视化图表，自动加载已有实体。
2. 能流畅从一个实体向另一个实体划线建立关系。
3. 界面保存时不报错，并且能够自动进行强类型检查，满足等保2.0审计与防重复提交（Idempotency-Key）。
4. npm run vue-tsc 不出现 any 等类型错误。
