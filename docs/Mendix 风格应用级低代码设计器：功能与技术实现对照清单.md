# Mendix 风格应用级低代码设计器：功能与技术实现对照清单

本文档基于对 Mendix 核心设计器功能的深入研究，结合 Atlas Security Platform 现有的技术栈（Vue 3 + AMIS 6.x + .NET 10 + SqlSugar），梳理出一份完整的功能与技术实现对照清单。此清单可作为后续设计器落地的需求基线与技术架构指南。

## 1. 页面设计器（Page Editor）

页面设计器是低代码平台最直观的入口，负责 UI 布局与组件编排。

| 子功能点 | Mendix 能力描述 | SecurityPlatform 实现技术方案 | 难度 | 优先级 |
|---|---|---|---|---|
| **画布 (Canvas)** | 支持结构模式（逻辑关系）与设计模式（所见即所得），提供拖拽放置区。 | 基于 `amis-editor` 的可视化拖拽能力，利用 AMIS `Grid`/`Flex` 布局。模式切换通过前端控制 AMIS Schema 的渲染模式实现。 | 中 | P0 |
| **组件面板 (Toolbox)** | 提供丰富的预定义组件（数据视图、网格等）及搜索功能。 | 封装 Ant Design Vue 4 组件及现有业务组件（如 `DynamicTable`），注册为 `amis-editor` 自定义插件。 | 中 | P0 |
| **属性面板 (Properties)** | 查看/编辑选中组件的属性与样式，支持数据绑定。 | 利用 `amis-editor` 自带属性配置，针对复杂业务属性开发自定义 Vue 3 组件作为属性编辑器嵌入。 | 中 | P0 |
| **组件树 (Component Tree)** | 层级结构展示页面组件，支持选中联动与拖拽排序。 | 复用 `amis-editor` 内置组件结构树，实现与画布的双向联动及拖拽排序。 | 低 | P1 |
| **多设备预览** | 预览页面在桌面、平板、手机等不同屏幕尺寸下的响应式效果。 | 基于 AMIS 响应式能力，前端实现设备切换器动态调整画布容器宽高。 | 中 | P1 |
| **撤销/重做** | 无限次历史记录回溯（Ctrl+Z / Ctrl+Y）。 | 基于 Vue 3 (Pinia) 维护 AMIS Schema 的历史栈，采用命令模式封装修改操作。 | 高 | P0 |

## 2. 数据模型与绑定（Domain Model & Data Binding）

Mendix 的核心优势在于强类型的数据模型驱动。

| 子功能点 | Mendix 能力描述 | SecurityPlatform 实现技术方案 | 难度 | 优先级 |
|---|---|---|---|---|
| **实体建模** | 可视化定义实体（表）、属性（字段）及类型。 | 前端基于 `amis-editor` 插件提供可视化建模；后端扩展 `LowCodeEntity` 实体，通过 `SqlSugar` CodeFirst 动态建表。 | 中 | P0 |
| **属性类型** | 支持字符串、整数、布尔、枚举、文件等内置类型。 | 前端映射为 AMIS 输入组件；后端映射为 .NET/SqlSugar 对应数据库类型。 | 低 | P0 |
| **关联关系** | 可视化建立一对多（1-N）、多对多（N-M）关系。 | 前端提供连线交互；后端利用 `SqlSugar` 导航属性自动处理外键与中间表。 | 中 | P1 |
| **数据视图 (Data View)** | 绑定单条记录上下文，组件自动绑定该记录属性。 | 映射为 AMIS `Form`/`Service` 组件，自动注入 `${id}` 变量并调用通用单条查询 API。 | 低 | P0 |
| **数据网格 (Data Grid)** | 绑定对象列表，提供分页、排序、搜索。 | 映射为 AMIS `CRUD` 组件，复用并扩展现有 `DynamicTable` 列表查询 API。 | 中 | P0 |
| **XPath 表达式** | 用于数据过滤与检索的强大查询语言。 | 前端提供表达式编辑器；后端实现 XPath 到 `SqlSugar` LINQ/SQL 的解析转换（具挑战性）。 | 高 | P2 |

## 3. 登录与用户上下文（Authentication & User Context）

安全与上下文感知是企业级应用的基础。

| 子功能点 | Mendix 能力描述 | SecurityPlatform 实现技术方案 | 难度 | 优先级 |
|---|---|---|---|---|
| **登录页设计** | 拖拽认证组件快速构建登录界面，支持自定义逻辑。 | 使用 AMIS Schema 渲染登录表单，结合 Ant Design Vue 组件，对接后端 JWT 认证 API。 | 中 | P0 |
| **当前用户信息** | `$currentUser` 系统变量，随处获取当前登录用户详情。 | 后端通过 JWT 解析注入请求上下文；前端通过全局状态（Pinia）存储，并在 AMIS 环境中暴露 `global.currentUser` 变量。 | 低 | P0 |
| **角色与权限控制** | 基于用户角色与模块角色的细粒度访问控制。 | 后端基于 ASP.NET Core `[Authorize]` 及 `SqlSugar` 拦截器实现 API 与数据权限；前端 AMIS 通过 `visibleOn` 结合角色控制 UI。 | 中 | P0 |
| **多租户上下文** | 确保用户只能看到其所属租户的数据，实现数据隔离。 | 后端通过中间件获取 `TenantId`，利用 `SqlSugar` 全局过滤器自动追加租户条件，实现逻辑隔离。 | 高 | P1 |

## 4. 复杂表格与增删改查（Complex CRUD & Data Grid）

企业应用中最核心的交互模式。

| 子功能点 | Mendix 能力描述 | SecurityPlatform 实现技术方案 | 难度 | 优先级 |
|---|---|---|---|---|
| **列配置** | 显隐、宽窄、排序、类型渲染的动态配置。 | 利用 `amis-editor` 配置 AMIS `Table` 组件的 `columns` 属性，持久化到页面 Schema 中。 | 中 | P0 |
| **排序/筛选/分页** | 内置的数据操作，触发后端重新查询。 | AMIS `Table` 原生支持，后端 API 结合 `SqlSugar` 的 `OrderBy`/`Where`/`ToPageList` 动态构建 SQL。 | 低 | P0 |
| **行内编辑** | 在网格行内直接编辑并保存数据。 | AMIS `Table` 开启 `editable` 模式，配置 `saveAs` 动作调用后端单行/多行更新 API。 | 中 | P1 |
| **批量操作** | 多选行后执行批量删除、修改状态等。 | AMIS `Table` 开启 `selectable`，通过顶部按钮触发批量 API，后端使用 `SqlSugar` 批量更新/删除。 | 中 | P1 |
| **主子表联动** | 选中主表行，动态加载关联子表数据。 | AMIS 嵌套 `Table` 或分栏布局，监听主表 `onEvent` 动态更新子表数据源（传递主表 ID）。 | 高 | P2 |

## 5. 表单设计（Form Design）

数据录入与验证的核心。

| 子功能点 | Mendix 能力描述 | SecurityPlatform 实现技术方案 | 难度 | 优先级 |
|---|---|---|---|---|
| **字段类型映射** | 丰富的输入元素绑定实体属性。 | AMIS Schema 映射（如 `input-text`, `input-date`, `input-file`），结合 Ant Design Vue 复杂组件。 | 低 | P0 |
| **字段联动与可见性** | 基于条件表达式的动态显示/隐藏/禁用。 | 利用 AMIS `visibleOn`, `disabledOn` 等表达式属性，或通过 `onEvent` 触发自定义逻辑。 | 中 | P0 |
| **表单校验** | 内置必填、正则、范围等校验规则。 | 前端依赖 AMIS Schema 校验规则；后端使用 FluentValidation 进行严格的服务端校验。 | 中 | P0 |
| **多步骤表单 (Wizard)** | 分步引导的数据录入。 | 使用 AMIS `Wizard` 组件，前端暂存步骤数据，最后一步统一提交后端持久化。 | 中 | P1 |
| **表单数据持久化** | 自动映射实体并保存至数据库，支持事务。 | 前端通过 AMIS `api` 提交 JSON，后端 API 结合 `SqlSugar` 映射实体并执行持久化，包含事务控制。 | 低 | P0 |

## 6. 业务逻辑与动作流（Microflow / Nanoflow / Action）

将静态页面转化为动态应用的关键。

| 子功能点 | Mendix 能力描述 | SecurityPlatform 实现技术方案 | 难度 | 优先级 |
|---|---|---|---|---|
| **按钮动作配置** | 可视化配置按钮点击触发的动作（调用微流、打开页面等）。 | 扩展 AMIS `Action` 组件，在属性面板提供预定义动作类型（如 `callMicroflow`），触发后端 API。 | 中 | P0 |
| **条件分支与循环** | 逻辑流中的条件判断与集合遍历。 | 前端集成流程设计器（如基于 `WorkflowCore` 可视化）；后端解析表达式并执行通用循环/分支逻辑。 | 中 | P0/P1 |
| **调用 REST API** | 与外部系统集成的标准方式。 | 设计器提供 API 配置节点；后端使用 `HttpClient` 发起请求，支持网关代理与认证。 | 中 | P0 |
| **触发工作流/审批流** | 无缝集成复杂的业务协作流程。 | 设计器配置触发节点；后端调用现有的 `WorkflowCore` 或 `ApprovalFlow` 引擎启动实例。 | 中 | P0 |
| **错误处理** | 异常捕获与自定义错误处理路径。 | 后端统一 `try-catch` 机制，根据配置执行回滚/日志记录；前端 AMIS 展示友好错误提示。 | 中 | P1 |

## 7. 导航与路由（Navigation & Routing）

| 子功能点 | Mendix 能力描述 | SecurityPlatform 实现技术方案 | 难度 | 优先级 |
|---|---|---|---|---|
| **菜单配置** | 直观的导航编辑器，支持权限控制。 | 基于 Ant Design Vue `Menu` 组件，后端提供菜单 API，结合 JWT 角色进行权限过滤。 | 中 | P0 |
| **页面跳转与参数** | 多种跳转方式（替换、弹窗）及参数传递。 | 依赖 Vue Router 4，支持 URL 参数与状态管理（Pinia）；AMIS 内部通过 `link` 动作或 `Modal` 组件实现。 | 低 | P0 |
| **权限控制路由** | 基于角色的页面访问控制。 | Vue Router 全局守卫结合 JWT 角色验证；后端 API 通过 `[Authorize]` 保护。 | 高 | P0 |

## 8. 发布与版本管理（Deployment & Version Control）

| 子功能点 | Mendix 能力描述 | SecurityPlatform 实现技术方案 | 难度 | 优先级 |
|---|---|---|---|---|
| **状态与快照** | 草稿/发布状态管理，基于版本控制的快照。 | `LowCodeApp/Page` 实体增加状态字段；后端集成 Git 仓库存储 Schema 快照，数据库记录元数据。 | 高 | P0 |
| **回滚** | 还原至先前版本。 | 后端调用 Git API 回滚 Schema，结合数据库备份策略处理数据模型回滚。 | 高 | P1 |
| **多环境部署** | 开发/测试/生产环境隔离。 | ASP.NET Core `appsettings` 多环境配置，结合 CI/CD 工具实现自动化部署。 | 中 | P0 |
| **导入导出** | 应用包（.mpk）的迁移与共享。 | 后端打包/解析包含 Schema、模型、流程定义的 ZIP 文件，处理实体同步与冲突。 | 高 | P1 |

---
*注：本清单中的 P0 代表核心基础能力，P1 代表进阶能力，P2 代表高级增强能力。建议在实施时按照优先级分阶段迭代。*
