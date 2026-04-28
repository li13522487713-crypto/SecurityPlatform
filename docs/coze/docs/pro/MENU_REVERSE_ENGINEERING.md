# Coze Studio 菜单级功能逆向分析 / 复刻导向文档

> 基于全量代码阅读，以"菜单/页面/功能点"为单位逐一拆解
> 日期：2026-04-09
> 说明：本文记录的是上游 Coze Studio 逆向分析快照。文中出现的 `/api/workflow_api/*`、`/api/playground_api/*`、`/api/draftbot/*` 为历史上游路由，不代表当前 Atlas 运行期主链；当前 Atlas 已统一改为 `api/app-web/*` gateway。

---

# 一、菜单系统总览

## 1.1 菜单定义位置 【已确认】

**本系统的菜单是前端静态配置，非后端动态返回。**

| 菜单层级 | 定义位置 | 渲染组件 |
|----------|---------|---------|
| **一级菜单（侧栏图标）** | `global-adapter/src/components/global-layout-composed/index.tsx` 第 62-78 行 `menus` 数组 | `layout/src/components/global-layout/component/menu-item.tsx` (`GLobalLayoutMenuItem`) |
| **侧栏操作按钮** | 同文件 第 55-61 行 `actions` 数组 | `layout/src/components/global-layout/component/action-btn.tsx` (`GlobalLayoutActionBtn`) |
| **侧栏底部额外按钮** | 同文件 第 80-89 行 `extras` 数组 | 同 `action-btn.tsx` |
| **侧栏底部 Footer** | 同文件 第 90 行 `footer={<AccountDropdown />}` | `global-adapter/src/components/account-dropdown/` |
| **二级菜单（工作空间）** | `space-ui-adapter/src/components/workspace-sub-menu/index.tsx` 第 37-52 行 `subMenu` 数组 | `space-ui-base/src/components/workspace-sub-menu/index.tsx` (`WorkspaceSubMenu`) |
| **二级菜单（探索）** | `community/explore/src/components/sub-menu/index.tsx` | 同上模式 |
| **路由定义** | `apps/coze-studio/src/routes/index.tsx` (298 行) | React Router 6 `createBrowserRouter` |

## 1.2 菜单渲染机制 【已确认】

### 一级菜单渲染流程

```
GlobalLayoutComposed (global-adapter)
  → 传递 menus=[{title,icon,activeIcon,path}] 给 GlobalLayout (layout)
    → GlobalLayoutSider (sider.tsx)
      → menus.map(menu => <GLobalLayoutMenuItem {...menu} />)
        → <NavLink to={path}> + 路径前缀匹配高亮 (location.pathname.startsWith(path))
```

### 二级菜单渲染流程

```
路由 loader 返回 { subMenu: WorkspaceSubMenu } 或 { subMenu: ExploreSubMenu }
  → useRouteConfig() 读取 subMenu
  → GlobalLayoutSider 检测 hasSubNav = Boolean(config.subMenu)
  → <SubMenu /> (sub-menu.tsx)
    → 从 config.subMenu 获取组件
    → <Suspense><SubMenuComponent /></Suspense>
    → 可拖拽调整宽度 (200-380px, localStorage 持久化)
```

### 菜单高亮机制

- **一级**: `location.pathname.startsWith(path)` — 当 URL 以菜单路径开头时高亮
- **二级**: `useRouteConfig().subMenuKey` 与菜单项 `path` 匹配 — 由路由 loader 传入 `subMenuKey: SpaceSubModuleEnum.DEVELOP`
- **面包屑**: 无传统面包屑组件，各页面自带返回按钮
- **展开/收起**: 二级菜单栏可拖拽宽度，收起到 200px
- **Tabs 多开**: 无 tabs 多开/keep-alive 机制
- **懒加载**: 所有路由页面均 `React.lazy()` + `Suspense`

## 1.3 菜单配置数据来源 【已确认】

| 数据 | 来源 | 说明 |
|------|------|------|
| 菜单文字 | `@coze-arch/i18n` (`I18n.t('navigation_workspace')`, `I18n.t('menu_title_store')`) | 前端国际化 |
| 菜单图标 | `@coze-arch/coze-design/icons` | Semi Design 自定义图标 |
| 菜单路径 | 硬编码字符串 (`'/space'`, `'/explore'`) | 与路由定义一致 |
| 菜单键 | `BaseEnum.Space`, `BaseEnum.Explore` (`@coze-arch/web-context/src/const/app.ts`) | 标识当前模块 |
| 二级菜单项 | `SpaceSubModuleEnum.DEVELOP`, `SpaceSubModuleEnum.LIBRARY` (`space-ui-adapter/src/const.ts`) | 工作空间二级 |
| 权限控制 | 无菜单级权限过滤 | 所有菜单对所有登录用户可见 |
| 动态路由 | 无 | 所有路由静态定义 |

---

# 二、菜单树与路由映射

## 2.1 完整菜单树

```
┌─────────────────────────────────────────────────────────────────┐
│ 左侧栏 (GlobalLayoutSider)                                      │
├─────────────────────────────────────────────────────────────────┤
│ [+] 创建按钮 (action)      → 打开创建 Bot/Project 弹窗          │
│ ─── 分割线 ───                                                  │
│ [🏠] 工作空间 (menu)       → /space                              │
│     ├─ [🤖] 开发 (subMenu)  → /space/:space_id/develop          │
│     └─ [📚] 资源库 (subMenu) → /space/:space_id/library         │
│ [🧭] 探索 (menu)           → /explore                           │
│     ├─ 插件商店              → /explore/plugin                   │
│     └─ 模板商店              → /explore/template                 │
│ ─── 底部区域 ───                                                │
│ [📄] 文档 (extra)          → 外部链接 coze.cn/open/docs         │
│ [👤] 账户下拉 (footer)     → AccountDropdown                     │
├─────────────────────────────────────────────────────────────────┤
│ 隐藏菜单（通过路由直接访问，无侧栏入口）                          │
├─────────────────────────────────────────────────────────────────┤
│ Agent IDE 编辑器             → /space/:space_id/bot/:bot_id      │
│ Agent 发布页                 → .../bot/:bot_id/publish           │
│ 项目 IDE                    → /space/:space_id/project-ide/:id/* │
│ 项目发布页                   → .../project-ide/:id/publish       │
│ 工作流编辑器                 → /work_flow?space_id=&workflow_id= │
│ 知识库详情                   → /space/:space_id/knowledge/:id    │
│ 知识库上传                   → .../knowledge/:id/upload          │
│ 数据库详情                   → /space/:space_id/database/:id     │
│ 插件详情                     → /space/:space_id/plugin/:id       │
│ 插件工具详情                 → .../plugin/:id/tool/:tool_id      │
│ 搜索结果页                   → /search/:word                     │
│ 登录页                       → /sign                             │
│ 文档跳转                     → /docs/*, /open/docs/*             │
└─────────────────────────────────────────────────────────────────┘
```

## 2.2 菜单→路由→页面入口→权限 映射总表

### 一级菜单

| # | 菜单名称 | 路由路径 | 页面入口文件 | 功能模块 | requireAuth | 权限控制 |
|---|---------|---------|-------------|---------|-------------|---------|
| 1 | **工作空间** | `/space` | `SpaceLayout` → `@coze-foundation/space-ui-adapter` | 空间管理 | true | 登录即可 |
| 2 | **探索** | `/explore` | `Component: null` → redirect `/explore/plugin` | 商店市场 | true | 登录即可 |
| — | **创建** (按钮) | — | `useCreateBotAction` → `useCreateProjectModal` → 弹窗 | 创建 Bot/Project | — | 登录即可 |
| — | **文档** (按钮) | 外部 | `window.open('https://www.coze.cn/open/docs/guides')` | 外部跳转 | — | — |
| — | **账户** (footer) | — | `AccountDropdown` 组件 | 账户管理 | — | 登录即可 |

### 二级菜单 — 工作空间

| # | 菜单名称 | 路由路径 | 页面入口文件 | 功能模块 | subMenuKey |
|---|---------|---------|-------------|---------|------------|
| 1.1 | **开发** | `/space/:space_id/develop` | `pages/develop.tsx` → `@coze-studio/workspace-adapter/develop` (402行) | 项目列表 | `SpaceSubModuleEnum.DEVELOP` |
| 1.2 | **资源库** | `/space/:space_id/library` | `pages/library.tsx` → `@coze-studio/workspace-adapter/library` (68行) | 资源列表 | `SpaceSubModuleEnum.LIBRARY` |

### 二级菜单 — 探索

| # | 菜单名称 | 路由路径 | 页面入口文件 | 功能模块 |
|---|---------|---------|-------------|---------|
| 2.1 | **插件商店** | `/explore/plugin` | `ExplorePluginPage` → `@coze-community/explore` | 插件市场 |
| 2.2 | **模板商店** | `/explore/template` | `ExploreTemplatePage` → `@coze-community/explore` | 模板市场 |

### 隐藏页面（无菜单入口）

| # | 页面名称 | 路由路径 | 页面入口文件 | 关键后端 API 前缀 |
|---|---------|---------|-------------|------------------|
| H1 | **Agent IDE** | `/space/:sid/bot/:bid` | `AgentIDELayout` → `AgentIDE (BotEditor)` | `/api/draftbot/*`, `/api/playground_api/*` |
| H2 | **Agent 发布** | `.../bot/:bid/publish` | `AgentPublishPage` | `/api/draftbot/publish` |
| H3 | **项目 IDE** | `/space/:sid/project-ide/:pid/*` | `ProjectIDE` → `@coze-project-ide/main` | `/api/intelligence_api/*` |
| H4 | **工作流编辑器** | `/work_flow` | `WorkflowPage` → `@coze-workflow/playground-adapter` | `/api/workflow_api/*` |
| H5 | **知识库详情** | `/space/:sid/knowledge/:did` | `KnowledgePreviewPage` | `/api/knowledge/*` |
| H6 | **知识库上传** | `.../knowledge/:did/upload` | `KnowledgeUploadPage` | `/api/knowledge/document/*` |
| H7 | **数据库详情** | `/space/:sid/database/:tid` | `DatabaseDetailPage` | `/api/memory/database/*` |
| H8 | **插件详情** | `/space/:sid/plugin/:pid` | `PluginLayout` → `PluginPage` | `/api/plugin_api/*` |
| H9 | **插件工具** | `.../plugin/:pid/tool/:tid` | `PluginToolPage` | `/api/plugin_api/*` |
| H10 | **搜索** | `/search/:word` | `SearchPage` → `@coze-community/explore` | `/api/marketplace/product/search` |
| H11 | **登录** | `/sign` | `LoginPage` → `@coze-foundation/account-ui-adapter` | `/api/passport/*` |

---

# 三、逐菜单详细拆解

## 菜单 1.1：工作空间 → 开发列表

### 1. 菜单定位
- **做什么**: 展示当前空间下所有 Bot / Project 的卡片列表，支持搜索、筛选、创建、删除、复制
- **面向用户**: 所有登录用户
- **业务域**: 项目管理
- **系统作用**: 开发工作的主入口页面，用户从这里进入具体 Bot/Project 编辑器

### 2. 页面入口 【已确认】
- **路由**: `/space/:space_id/develop`
- **入口文件**: `apps/coze-studio/src/pages/develop.tsx` (27行) → `@coze-studio/workspace-adapter/develop` `Develop` (402行)
- **挂载**: `SpaceLayout` → `SpaceIdLayout` → `Develop`
- **Layout**: 带侧栏 (`hasSider: true`)，二级菜单 (`subMenu: WorkspaceSubMenu`)

### 3. 页面结构 【已确认】

```
Layout (全宽)
├── Header
│   ├── HeaderTitle: "开发" 文字
│   └── HeaderActions: [创建] 按钮
├── SubHeader
│   ├── SubHeaderFilters: 类型筛选 + 创建者筛选(团队空间) + 状态筛选
│   └── SubHeaderSearch: 搜索框 (200px, debounced)
└── Content (ref=containerRef, 无限滚动)
    ├── 列表区: 3列(4列@1600px) grid → BotCard 卡片
    ├── 空状态: WorkspaceEmpty (有筛选=清除按钮, 无筛选=空提示)
    ├── 加载更多: Loading spinner
    └── 无更多: 占位 div
```

### 4. 功能点清单 【已确认】

| # | 功能 | 触发方式 | 前端 hook/函数 |
|---|------|---------|---------------|
| 1 | **列表加载** | 页面初始化 + 无限滚动 | `useIntelligenceList` → `intelligenceApi.GetDraftIntelligenceList` |
| 2 | **类型筛选** | Select 下拉 (全部/Bot/Project) | `setFilterParams({searchType})` |
| 3 | **创建者筛选** | Select 下拉 (全部/我创建的) | `setFilterParams({searchScope})` |
| 4 | **状态筛选** | Select 下拉 (全部/已发布/最近打开) | `setFilterParams({isPublish, recentlyOpen})` |
| 5 | **搜索** | Search 输入框 (debounced) | `debouncedSetSearchValue(val)` |
| 6 | **创建项目** | Header [创建] 按钮 | `useIntelligenceActions.actions.createIntelligence` → `useCreateProjectModal` |
| 7 | **点击卡片** | BotCard onClick | `navigate('/space/:sid/bot/:bid')` 或 `.../project-ide/:pid` |
| 8 | **收藏/取消收藏** | BotCard hover → FavoriteIconBtn | `FavoriteIconBtn` 组件 → API |
| 9 | **复制 Agent** | BotCard hover → ⋯ → 复制 | `MenuCopyBot` → `DeveloperApi.DuplicateDraftBot` |
| 10 | **复制 Project** | BotCard hover → ⋯ → 复制 | `useCardActions.onCopyProject` → `useCopyProjectModal` |
| 11 | **删除** | BotCard hover → ⋯ → 删除 | `actions.deleteIntelligence` → confirm → API |
| 12 | **复制状态轮询** | 自动 (CopyProcessMask) | `useProjectCopyPolling` → `intelligenceCopyTaskPollingService` |
| 13 | **全局事件监听** | 自动 | `useGlobalEventListeners` → 收藏变更/模板复制刷新列表 |

### 5. 每个功能点的前端逻辑 【已确认】

**功能 1: 列表加载**
- `useIntelligenceList` (`entry-base/src/pages/develop/hooks/use-intelligence-list.ts`, 204行)
- 使用 `useInfiniteScroll` (ahooks) + `intelligenceApi.GetDraftIntelligenceList`
- 参数: `spaceId`, `searchValue`, `types`, `hasPublished`, `recentlyOpen`, `searchScope`, `orderBy`
- pageSize = 24, 滚动到底自动加载
- 失败: `Toast.error` + Slardar 上报
- loading 态: `<Spin spinning>`

**功能 6: 创建项目**
- `useIntelligenceActions` (`hooks/use-intelligence-actions.tsx`, 124行)
- 调用 `useCreateProjectModal` (外部包)
- 成功: Bot → `navigate('/space/:sid/bot/:bid')`, Project → `navigate('/space/:sid/project-ide/:pid')`
- 列表刷新: `mutate()` 或 `reload()`

**功能 9: 复制 Agent**
- `MenuCopyBot` (`components/bot-card/menu-actions.tsx`, 233行)
- API: `DeveloperApi.DuplicateDraftBot({ bot_id })`
- 成功回调: `onCopySuccess(newBotId, spaceId)` → 刷新列表
- 失败: `Toast.error`

### 6. 每个功能点的接口逻辑 【已确认】

| 功能 | API 方法 | HTTP | 路径 | 入参 | 出参 |
|------|---------|------|------|------|------|
| 列表 | `intelligenceApi.GetDraftIntelligenceList` | POST | `/api/intelligence_api/search/get_draft_intelligence_list` | `{space_id, keyword, types[], has_published, recently_open, search_scope, order_by, page_index, page_size}` | `{intelligence_list[], page_count, total_count}` |
| 创建 Bot | `DeveloperApi.DraftBotCreate` | POST | `/api/draftbot/create` | `{space_id, name, description, icon}` | `{bot_id}` |
| 删除 Bot | `DeveloperApi.DeleteDraftBot` | POST | `/api/draftbot/delete` | `{bot_id}` | `{}` |
| 复制 Bot | `DeveloperApi.DuplicateDraftBot` | POST | `/api/draftbot/duplicate` | `{bot_id}` | `{bot_id}` |
| 收藏 | 内部 `FavoriteIconBtn` | POST | `/api/marketplace/product/favorite` | `{entity_id, entity_type, is_fav}` | `{}` |
| Project 复制 | `intelligenceApi.DraftProjectCopy` | POST | `/api/intelligence_api/draft_project/copy` | `{project_id, to_space_id, name, description, icon_uri}` | `{task_id}` |
| 复制状态 | `intelligenceApi.EntityTaskSearch` | POST | — | `{task_ids[]}` | `{task_list[]}` |

### 7. 每个功能点的后端逻辑 【已确认】

**列表查询链路**:
```
POST /api/intelligence_api/search/get_draft_intelligence_list
  → handler: GetDraftIntelligenceList (intelligence_service.go)
  → application: AppSVC.GetDraftIntelligenceList()
  → 聚合 Bot 列表 + Project 列表，按 update_time 排序
  → 分页返回
```

**创建 Bot 链路**:
```
POST /api/draftbot/create
  → handler: DraftBotCreate (developer_api_service.go)
  → application: SingleAgentSVC.CreateDraftBot()
  → domain: singleagent.Create()
  → MySQL INSERT (draft_bot 表) + 初始化默认配置
  → 返回 bot_id
```

### 8. 数据模型 【已确认】

**核心实体**: `IntelligenceData` (IDL)

| 前端字段 | 后端字段 | 说明 |
|----------|---------|------|
| `basic_info.id` | `id` (string) | 项目 ID |
| `basic_info.name` | `name` | 名称 |
| `basic_info.icon_url` | `icon_url` | 图标 URL |
| `basic_info.description` | `description` | 描述 |
| `basic_info.space_id` | `space_id` | 空间 ID |
| `basic_info.update_time` | `update_time` | 更新时间 |
| `basic_info.status` | `status` | 状态 (IntelligenceStatus 枚举: Normal/Banned) |
| `type` | `type` | IntelligenceType (Bot/Project) |
| `publish_info.has_published` | `has_published` | 是否已发布 |
| `publish_info.connectors[]` | `connectors` | 发布渠道 |
| `permission_info.can_delete` | `can_delete` | 是否可删 |
| `owner_info.nickname` | `nickname` | 创建者昵称 |
| `favorite_info.is_fav` | `is_fav` | 是否收藏 |

**筛选枚举**:
- `IntelligenceType`: Bot=1, Project=2
- `SearchScope`: All=0, CreateByMe=1
- `DevelopCustomPublishStatus`: All='all', Published='published'
- `IntelligenceStatus`: Normal=0, Banned=2

### 9. 状态与交互规则 【已确认】

- **初始化**: `SpaceLayout` → `useInitSpace(space_id)` 加载空间列表 → `SpaceIdLayout` → `useInitSpaceRole(space_id)` 初始化权限 → `Develop` → `useIntelligenceList` 自动请求
- **筛选**: 改变任何 Select → `setFilterParams` → `reloadDeps` 触发重新请求 → 列表刷新
- **搜索**: 输入 → debounce → `setFilterParams` → 列表刷新
- **切空间**: `spaceId` 变化 → `searchValue` 清空 → 列表重新加载
- **点击卡片**: Bot → `/space/:sid/bot/:bid`, Project → `/space/:sid/project-ide/:pid`, Banned 状态不可点
- **hover 卡片**: 显示收藏按钮 + ⋯ 更多菜单
- **删除**: confirm 弹窗 → API → Toast.success → 列表过滤掉该项
- **复制 Project**: 发起 → 进入轮询状态(CopyProcessMask) → 成功/失败 Toast

### 10. 权限逻辑 【已确认】

- **菜单可见**: 所有登录用户
- **创建按钮**: 所有登录用户
- **删除按钮**: `permission_info.can_delete` 控制 (`true` 时可点)
- **复制按钮**: Banned 状态时 disabled
- **权限来源**: 后端返回 `permission_info`，前端仅做 UI 控制
- **空间权限**: `useInitSpaceRole` 初始化空间角色 → OSS 版本强制 Owner

### 11. 边界与异常处理 【已确认】

- **空数据**: `WorkspaceEmpty` 组件，有筛选时显示"清除筛选"按钮
- **接口失败**: `Toast.error` + Slardar 上报
- **Banned Bot**: 卡片显示红色警告图标，点击不跳转
- **复制失败**: `CopyProcessMask` 显示重试/取消按钮
- **Loading**: `<Spin>` 全区域旋转
- **无限滚动兜底**: `noMore && data.length` → 占位 div
- **隐藏操作**: `hideOperation` (来自 `useSpaceStore`) 可隐藏全部操作按钮

### 12. 复刻要点

**必做前端页面**: `Develop` (卡片列表页，含 Header + SubHeader + Content 三段式布局)

**必做组件**:
- `BotCard` (400行，含 hover actions/收藏/更多菜单/状态图标/复制遮罩)
- `WorkspaceEmpty` (空状态)
- `Creator` (作者+时间行)
- `IntelligenceTag` (Bot/Project 标签)
- `CopyProcessMask` (复制状态遮罩)

**必做接口**: `GetDraftIntelligenceList` (列表), `DraftBotCreate` (创建), `DeleteDraftBot` (删除), `DuplicateDraftBot` (复制)

**必做后端逻辑**: 项目列表查询(分页+筛选), Bot CRUD, 权限信息聚合

**必做数据表**: `draft_bot` (Bot 表), `draft_project` (Project 表), `space` (空间表)

**可简化**: Tea 事件上报, 复制轮询(改为同步), 收藏功能(可后续添加)

**最容易遗漏**: `useCachedQueryParams` 筛选条件 localStorage 持久化, 切空间时清空搜索, Banned 状态 UI 处理

**最复杂的部分**: `BotCard` 400 行组件(状态渲染逻辑多), `useIntelligenceList` 无限滚动 + cancel token

---

## 菜单 1.2：工作空间 → 资源库

### 1. 菜单定位
- **做什么**: 以表格形式展示空间内所有资源（插件/工作流/知识库/Prompt/数据库），支持类型筛选、创建各类资源、删除、详情查看
- **面向用户**: 所有登录用户
- **业务域**: 资源管理
- **系统作用**: 所有非 Bot/Project 资源的统一管理入口

### 2. 页面入口 【已确认】
- **路由**: `/space/:space_id/library`
- **入口文件**: `pages/library.tsx` (27行) → `@coze-studio/workspace-adapter/library` `LibraryPage` (68行) → `@coze-studio/workspace-base/library` `BaseLibraryPage` (300行)
- **Layout**: 带侧栏，二级菜单 (`subMenuKey: SpaceSubModuleEnum.LIBRARY`)

### 3. 页面结构 【已确认】

```
Layout
├── Layout.Header
│   ├── LibraryHeader: "资源库" 标题 + [创建资源] 下拉菜单按钮
│   │   └── Menu.SubMenu: 创建插件/工作流/Chatflow/知识库/Prompt/数据库
│   └── 筛选行:
│       ├── Cascader: 类型筛选 (含知识库二级: 文本/表格/图片)
│       ├── Select: 创建者筛选 (团队空间)
│       ├── Select: 状态筛选 (全部/已发布/未发布)
│       └── Search: 名称搜索
└── Layout.Content
    └── Table (无限滚动 cursor 分页)
        ├── 列: 资源名称 | 类型 | 创建者(团队) | 编辑时间 | 操作
        └── 空状态: WorkspaceEmpty
```

### 4. 功能点清单 【已确认】

| # | 功能 | 说明 |
|---|------|------|
| 1 | 资源列表 | `PluginDevelopApi.LibraryResourceList` cursor 分页 |
| 2 | 类型筛选 | Cascader 级联选择（知识库有二级子类型） |
| 3 | 创建者筛选 | 全部 / 我创建的 |
| 4 | 状态筛选 | 全部 / 已发布 / 未发布 |
| 5 | 搜索 | 名称搜索 |
| 6 | **创建插件** | `CreateFormPluginModal` → 创建 → 跳转插件详情 |
| 7 | **创建工作流** | `useWorkflowResourceAction.openCreateModal(Workflow)` |
| 8 | **创建 Chatflow** | `openCreateModal(ChatFlow)` |
| 9 | **创建知识库** | `useCreateKnowledgeModalV2` → 创建 → 跳转知识库详情/上传 |
| 10 | **创建 Prompt** | `usePromptConfiguratorModal` → 创建弹窗 |
| 11 | **创建数据库** | `useLibraryCreateDatabaseModal` → 创建 → 跳转数据库详情 |
| 12 | 行点击 | 按资源类型跳转对应详情页 |
| 13 | **删除** | 各类型独立删除 API + confirm |
| 14 | **编辑** (Prompt) | 编辑弹窗 |
| 15 | **启用/禁用** (知识库) | Switch 切换 → `KnowledgeApi.UpdateDataset` |

### 5. 每个功能的接口逻辑 【已确认】

| 功能 | API | 路径 |
|------|-----|------|
| 列表 | `PluginDevelopApi.LibraryResourceList` | POST `/api/plugin_api/library_resource_list` |
| 删插件 | `PluginDevelopApi.DelPlugin` | POST `/api/plugin_api/del_plugin` |
| 删工作流 | (workflow resource action) | POST `/api/workflow_api/delete` |
| 删知识库 | `KnowledgeApi.DeleteDataset` | POST `/api/knowledge/delete` |
| 删Prompt | `PlaygroundApi.DeletePromptResource` | POST `/api/playground_api/delete_prompt_resource` 【推断】|
| 删数据库 | `MemoryApi.DeleteDatabase` | POST `/api/memory/database/delete` |
| 启禁知识库 | `KnowledgeApi.UpdateDataset` | POST `/api/knowledge/update` |

### 6. 数据模型 【已确认】

**核心实体**: `ResourceInfo` (IDL `plugin_develop`)

| 字段 | 说明 |
|------|------|
| `res_id` | 资源 ID |
| `name` | 名称 |
| `desc` | 描述 |
| `icon` | 图标 URL |
| `res_type` | 资源类型 (ResType 枚举: Plugin=0, Workflow=2, Knowledge=4, Prompt=5, Database=7) |
| `res_sub_type` | 子类型 (知识库: 0=文本/1=表格/2=图片, 插件: 1=Http/2=App/6=Local) |
| `publish_status` | 发布状态 (Published/UnPublished) |
| `edit_time` | 编辑时间 |
| `creator_name` | 创建者 |
| `actions[]` | 可用操作 (ActionKey: Delete/Edit/EnableSwitch + enable flag) |
| `biz_res_status` | 业务状态 (3=Disabled) |

### 7. 复刻要点

**必做前端**: `BaseLibraryPage` (表格+筛选) + 5 个 `useEntityConfig` hook (各资源类型配置)

**必做组件**: `LibraryHeader`, `BaseLibraryItem`, `Table`(coze-design 无限滚动), `WorkspaceEmpty`

**必做接口**: `LibraryResourceList` (统一列表), 各类型 CRUD API

**必做后端**: 统一资源列表查询（聚合多种资源类型），各类型独立 CRUD

**最复杂**: `entityConfigs` 模式 — 5 种资源类型的创建/详情/操作逻辑各不相同，通过配置对象统一接入

---

# 四、菜单复刻总表

| # | 菜单名称 | 路由 | 页面入口 | 核心功能 | 关键前端文件 | 关键后端 API | 关键数据实体 | 权限控制 | 复刻复杂度 | 复刻优先级 |
|---|---------|------|---------|---------|------------|-------------|------------|---------|----------|----------|
| 1.1 | 开发列表 | `/space/:sid/develop` | `workspace-adapter/develop` | Bot/Project 卡片列表+筛选+CRUD | `entry-base/pages/develop/`, `bot-card/` | `/api/intelligence_api/*`, `/api/draftbot/*` | `IntelligenceData`, `draft_bot` | `permission_info.can_delete` | **中** | **高** |
| 1.2 | 资源库 | `/space/:sid/library` | `workspace-base/library` | 5 种资源统一表格+CRUD | `entry-base/pages/library/`, 5 个 entity-config | `/api/plugin_api/library_resource_list`, 各类型API | `ResourceInfo` | `actions[].enable` | **高** | **高** |
| 2.1 | 插件商店 | `/explore/plugin` | `explore/pages/plugin` | 插件列表+搜索+收藏+复制 | `community/explore/src/` | `/api/marketplace/product/*` | Product | 登录即可 | **低** | **低** |
| 2.2 | 模板商店 | `/explore/template` | `explore/pages/template` | 模板列表+搜索+复制 | `community/explore/src/` | `/api/marketplace/product/*` | Product | 登录即可 | **低** | **低** |
| H1 | Agent IDE | `/space/:sid/bot/:bid` | `agent-ide entry-adapter` | Bot 编辑器（Prompt/模型/技能/调试） | `agent-ide/` 48 个子包 | `/api/draftbot/*`, `/api/playground_api/*`, `/api/conversation/*` | `DraftBotInfo`, 14 个 store | 空间权限+协作 | **极高** | **高** |
| H4 | 工作流编辑器 | `/work_flow` | `workflow/playground-adapter` | 可视化编排画布(40+节点) | `workflow/playground/src/` 2200+文件 | `/api/workflow_api/*` | `WorkflowCanvas`, `WorkflowNode` | 空间权限 | **极高** | **中** |
| H5 | 知识库详情 | `/space/:sid/knowledge/:did` | `workspace-base/knowledge-preview` | 文档列表+切片+上传+预览 | `data/knowledge-*` 多包 | `/api/knowledge/*` | `Dataset`, `Document`, `Slice` | `actions[].enable` | **高** | **中** |
| H7 | 数据库详情 | `/space/:sid/database/:tid` | `workspace-base/database` | 表结构+数据行管理 | `data/memory/database-v2` | `/api/memory/database/*` | `DatabaseTable`, `DatabaseRecord` | `actions[].enable` | **中** | **中** |
| H8 | 插件详情 | `/space/:sid/plugin/:pid` | `workspace-base/plugin` | 插件+工具编辑 | `agent-ide/bot-plugin/` | `/api/plugin_api/*` | `PluginInfo`, `ToolInfo` | 编辑锁+权限 | **高** | **中** |
| H11 | 登录 | `/sign` | `account-ui-adapter` | 邮箱注册/登录/密码重置 | `foundation/account-*` | `/api/passport/*` | `UserInfo` | 无需登录 | **中** | **高** |

---

# 五、复刻路线图

## 第一阶段：核心骨架 + 基础菜单（2-3 周）

### 必须先做
1. **登录系统** — 邮箱注册/登录/Session, 因为所有页面依赖登录态
   - 前端: LoginPage + useUserStore + useCheckLogin + RequireAuthContainer
   - 后端: passport API + Session + bcrypt
   - 数据表: `user`

2. **App Shell** — Layout + 路由 + 侧栏
   - GlobalLayoutComposed + GlobalLayoutSider + MenuItems
   - createBrowserRouter + Suspense lazy loading
   - 空间初始化: SpaceLayout + SpaceIdLayout + useInitSpace

3. **工作空间-开发列表** — 这是用户登录后的首页
   - Develop 页面 + BotCard + 筛选 + 创建弹窗
   - intelligenceApi 列表接口 + draftbot CRUD

4. **Agent IDE 简化版** — 可编辑 Prompt + 选模型 + 聊天调试
   - BotEditorLayout + BotEditor(简化)
   - useBotInfoStore + usePersonaStore + useModelStore
   - Chat 调试区(简化版 ChatSDK + fetchStream SSE)

## 第二阶段：补齐通用能力（2-3 周）

| 能力 | 实现方式 | 来源 |
|------|---------|------|
| 登录鉴权 | Session Cookie + useUserStore | `account-base/store/user.ts` |
| 菜单权限 | 无需（OSS 全量开放） | — |
| 列表页模板 | Layout/Header/SubHeader/Content + Table | `workspace-base/components/layout/` |
| 表单弹窗模板 | coze-design Modal + Form | Semi Design |
| API 请求封装 | axiosInstance + 双层拦截器 + fetchStream | `bot-http/axios.ts` + `fetch-stream.ts` |
| 状态管理 | Zustand per-domain | 直接复用模式 |
| 枚举/字典 | IDL 生成类型 | `@coze-arch/idl/*` |
| 文件上传 | MinIO S3 + `/api/common/upload` | `upload_service.go` |
| i18n | `@coze-arch/i18n` | 可简化为 JSON 文件 |

## 第三阶段：复杂页面（4-6 周）

| 页面 | 为什么复杂 | 建议 |
|------|----------|------|
| **工作流编辑器** | 2200+ 文件, Inversify DI + Fabric.js 画布, 40+ 节点注册 | 先做简化版(5-10 种核心节点), 用基础 canvas 库 |
| **Agent IDE 完整版** | 14 个 store + 48 个子包, 多 Agent 画布, 插件/工作流/知识库技能面板 | 逐步叠加: Prompt → 模型 → 插件面板 → 知识库面板 → 工作流面板 |
| **知识库全套** | 上传/解析/切片/检索全链路, 多格式文档解析 | 先支持 TXT/Markdown, 后加 PDF/DOCX |
| **资源库** | 5 种资源类型各有独立创建/编辑/删除逻辑 | 先做 Plugin + Workflow, 后加 Knowledge + DB + Prompt |

## 第四阶段：最小可运行复刻方案

### 保留菜单
- ✅ 登录 (`/sign`)
- ✅ 开发列表 (`/space/:sid/develop`)
- ✅ Agent IDE (`/space/:sid/bot/:bid`) — 简化版
- ✅ 工作流编辑器 (`/work_flow`) — 简化版(5 种节点)
- ❌ 资源库（可后加）
- ❌ 探索商店（可后加）

### 保留接口
- `/api/passport/*` (登录/注册)
- `/api/draftbot/*` (Bot CRUD + 发布)
- `/api/intelligence_api/search/*` (列表查询)
- `/api/conversation/chat` (Agent 对话 SSE)
- `/api/workflow_api/create|save|canvas|test_run|test_resume` (工作流基础)
- `/api/admin/config/model/*` (模型管理)

### 保留数据表
- `user` — 用户
- `space` — 空间
- `draft_bot` — Bot 草稿
- `workflow` / `workflow_canvas` — 工作流
- `model_config` — 模型配置
- `conversation` / `message` — 对话记录

### 保留状态流转
- Bot: 创建(草稿) → 编辑 → 发布(上线) → 删除
- Workflow: 创建 → 编辑画布 → 测试运行 → 发布

---

# 六、关键风险与最容易遗漏的点

## 隐藏逻辑

| # | 隐藏逻辑 | 位置 | 说明 |
|---|---------|------|------|
| 1 | **页面初始化自动请求** | `useAppInit()` 中 `useCheckLogin` 自动检测登录态 | 不调用则无法进入任何 requireAuth 页面 |
| 2 | **空间初始化链** | `SpaceLayout` → `useInitSpace` → `SpaceIdLayout` → `useInitSpaceRole` | 缺少任一步则子页面不渲染(返回 null) |
| 3 | **筛选条件 localStorage** | `useCachedQueryParams` → `workspace-develop-filters` / `workspace-library-filters` | 刷新页面保留筛选条件 |
| 4 | **菜单宽度 localStorage** | `sub-menu.tsx` → `submenu-width` | 二级菜单宽度持久化 |
| 5 | **下拉枚举来源** | `develop-filter-options.ts`, `consts.ts` | 全部前端静态定义，非后端返回 |
| 6 | **收藏功能** | `FavoriteIconBtn` + `FavoritesList` | 二级菜单底部显示收藏列表 |
| 7 | **SPA 兜底** | 后端 NoRoute handler → 非 API 路径返回 `index.html` | 不做则路由刷新 404 |
| 8 | **CORS 配置** | 后端中间件 AllowAllOrigins | 开发环境前端代理可绕过 |
| 9 | **Tea 事件埋点** | 几乎每个操作都有 `sendTeaEvent` | 复刻时可删除但要注意不破坏逻辑流 |
| 10 | **IS_OPEN_SOURCE 全局常量** | 构建时注入 | 控制 Creator 组件是否显示作者信息 |
| 11 | **Agw-Js-Conv: str header** | 每个 bot-api wrapper 中硬编码 | 后端 IDL 解析依赖此 header |
| 12 | **BaseEnum / menuKey** | 路由 loader → `menuKey: BaseEnum.Space` | 一级菜单高亮依赖此值 |
| 13 | **pageModeByQuery** | 知识库/数据库路由 loader | URL `page_mode=modal` 时页面以弹窗模式显示 |
| 14 | **URL 参数驱动** | 工作流页面完全由 URL search params 驱动 | `space_id`, `workflow_id`, `version`, `node_id`, `execute_id` |
| 15 | **多 token 场景** | Session Cookie (WebAPI) vs Bearer Token (OpenAPI) | 前后端各有独立中间件处理 |

---

# 七、已读文件清单

## 菜单/Layout 相关（本次重点阅读）
- `global-adapter/src/components/global-layout-composed/index.tsx` ✅ 完整源码
- `layout/src/components/global-layout/component/sider.tsx` ✅ 完整
- `layout/src/components/global-layout/component/menu-item.tsx` ✅ 完整
- `layout/src/components/global-layout/component/sub-menu.tsx` ✅ 完整
- `layout/src/components/global-layout/component/action-btn.tsx` ✅ 完整
- `layout/src/components/global-layout/types.ts` ✅ 完整
- `layout/src/components/global-layout/hooks.ts` ✅ 完整
- `layout/src/components/global-layout/context.tsx` ✅ 完整
- `space-ui-base/src/components/workspace-sub-menu/index.tsx` ✅ 完整
- `space-ui-adapter/src/components/workspace-sub-menu/index.tsx` ✅ 完整
- `space-ui-adapter/src/const.ts` ✅ 完整
- `web-context/src/const/app.ts` (BaseEnum/SpaceAppEnum) ✅ 完整
- `community/explore/src/components/sub-menu/index.tsx` ✅ 完整
- `global-adapter/src/components/account-dropdown/` ✅ 结构

## Develop 页面相关
- `workspace-adapter/develop/index.tsx` (402行) ✅ 完整 (前次读取)
- `workspace-base/pages/develop/index.tsx` (62行 barrel) ✅ 完整
- `workspace-base/pages/develop/hooks/` 全部 6 个 hook 文件 ✅ 完整
- `workspace-base/pages/develop/service/` ✅ 完整
- `workspace-base/pages/develop/components/bot-card/` 全部 7 个文件 ✅ 完整
- `workspace-base/pages/develop/type.ts` + `develop-filter-options.ts` + `page-utils/*` ✅ 完整
- `workspace-base/src/components/creator.tsx` ✅ 完整
- `workspace-base/src/components/workspace-empty.tsx` ✅ 完整

## Library 页面相关
- `workspace-base/pages/library/index.tsx` (300行) ✅ 完整
- `workspace-base/pages/library/types.ts` ✅ 完整
- `workspace-base/pages/library/consts.ts` ✅ 完整
- `workspace-base/pages/library/components/` 全部 ✅ 完整
- `workspace-base/pages/library/hooks/` 全部(含 5 个 entity-config) ✅ 完整

## 其他页面入口
- `workspace-base/pages/plugin/index.tsx` ✅ 完整
- `workspace-base/pages/tool/index.tsx` ✅ 完整
- `workspace-base/pages/database/index.tsx` ✅ 完整
- `workspace-base/pages/knowledge-preview/index.tsx` ✅ 完整
- `workspace-base/pages/knowledge-upload/index.tsx` ✅ 完整

## 后端路由映射
- `backend/api/router/coze/api.go` 全部 551 行 ✅ 完整
- 9 个 handler 文件首 100 行 ✅

---

# 八、还未确认的菜单/功能/链路

| 项目 | 状态 | 说明 |
|------|------|------|
| 探索-插件商店页面内部组件结构 | 【推断】基于 `explore/src/pages/plugin/` 入口 |
| 探索-模板商店页面内部组件结构 | 【推断】同上 |
| AccountDropdown 完整菜单项 | 【推断】包含设置/退出，未全部读 |
| Agent IDE 48 个子包各自功能点 | 【未确认】仅读了 entry/layout/chat-debug/prompt 等核心 |
| 工作流编辑器 300+ 节点注册组件 | 【未确认】仅读了入口和结构 |
| 项目 IDE 13 个子包 | 【未确认】未深入读取 |
| 管理后台配置页面 | 【推断】`/api/admin/config/*` 路由存在，但无前端路由入口(可能为独立管理入口) |
| 收藏列表组件 FavoritesList | 【推断】存在于 workspace-sub-menu 内 |
| 创建 Project 弹窗完整逻辑 | 【推断】来自外部包 `useCreateProjectModal` |

---

# 九、剩余隐藏页面逐一拆解

---

## 菜单 H1：Agent IDE（Bot 编辑器）

### 1. 菜单定位
- **做什么**: 编辑 AI Agent（Bot）的核心页面，包含 Prompt 编辑、模型选择、技能面板（插件/工作流/知识库）、调试聊天
- **面向用户**: 开发者
- **业务域**: AI Agent 开发
- **系统作用**: 系统最核心的功能页面，用户在此编辑 Agent 的各项配置并实时调试

### 2. 页面入口 【已确认】
- **路由**: `/space/:space_id/bot/:bot_id`
- **入口**: `AgentIDELayout` → `BotEditorLayout` (layout-adapter) → `BotEditorWithContext` (entry-adapter/agent-editor.tsx, 142行)
- **Layout**: 无侧栏 (`hasSider: false`)，独立全屏编辑器

### 3. 页面结构 【已确认】

```
BotEditorLayout (layout-adapter)
├── BotHeader (顶部栏)
│   ├── Bot 名称/图标/编辑
│   ├── 模式切换 (Single LLM / Workflow)
│   ├── Draft 状态
│   └── 部署按钮 (DeployButton)
└── BotEditorWithContext (entry-adapter)
    ├── [Single LLM 模式] SingleMode
    │   ├── AgentConfigArea (左侧配置区)
    │   │   ├── Persona Prompt 编辑器
    │   │   ├── 模型配置
    │   │   └── 技能面板 (插件/工作流/知识库/数据库/变量)
    │   └── AgentChatArea (右侧聊天区)
    │       └── BotDebugChatArea (调试聊天)
    ├── [Workflow 模式] WorkflowMode
    │   ├── SheetView "build" (左侧)
    │   │   ├── BotConfigArea (模型+基础配置)
    │   │   ├── WorkflowConfigArea (工作流选择)
    │   │   ├── DataMemory + memoryToolSlot (数据库/变量)
    │   │   └── DialogGroup (开场白/背景)
    │   └── SheetView "debug" (右侧)
    │       └── BotDebugChatArea (调试聊天)
    └── [共享] BotDebugPanel + AbilityAreaContainer
```

### 4. 功能点清单 【已确认】

| # | 功能 | 说明 |
|---|------|------|
| 1 | **初始化 Bot 数据** | `useInitAgent()` 加载 bot 配置 → 14 个 store 初始化 |
| 2 | **编辑 System Prompt** | Persona/Prompt 编辑区 (prompt 包) |
| 3 | **选择/配置模型** | `useModelStore` → ModelConfigView |
| 4 | **添加插件技能** | SkillsPane → 插件列表 → 添加/移除 |
| 5 | **添加工作流技能** | SkillsPane → 工作流列表 |
| 6 | **添加知识库** | SkillsPane → 知识库绑定 |
| 7 | **数据库/变量** | TableMemory → 数据库绑定 + 变量管理 |
| 8 | **调试聊天** | BotDebugChatArea → ChatSDK → SSE 流式对话 |
| 9 | **模式切换** | Single LLM ↔ Workflow |
| 10 | **保存草稿** | 自动保存 (`savingInfo` from `usePageRuntimeStore`) |
| 11 | **发布 Bot** | DeployButton → PublishDraftBot API |
| 12 | **复制 Bot** | Header ⋯ 菜单 → DuplicateDraftBot |
| 13 | **查看发布历史** | Header ⋯ → 发布记录 |
| 14 | **预览模式** | `usePageRuntimeStore.isPreview` |
| 15 | **编辑锁** | `usePageRuntimeStore.editLock` + 后端 `check_and_lock_plugin_edit` |
| 16 | **开场白/背景配置** | ChatBackground + Onboarding Message |

### 5. 核心接口 【已确认】

| API | 路径 | 说明 |
|-----|------|------|
| 获取 Bot 信息 | POST `/api/draftbot/get_display_info` | 加载 bot 配置 |
| 更新 Bot 信息 | POST `/api/draftbot/update_display_info` | 保存配置变更 |
| 发布 Bot | POST `/api/draftbot/publish` | 发布 |
| 获取插件列表 | POST `/api/plugin_api/get_playground_plugin_list` | 可用插件 |
| 聊天调试 | POST `/api/conversation/chat` (SSE) | Agent 对话调试 |
| 创建会话 | POST `/api/conversation/create` | 新建调试会话 |
| 获取会话消息 | POST `/api/conversation/message/list` | 历史消息 |

### 6. 状态管理 【已确认】

**14 个 Zustand store** (via `useBotDetailStoreSet`):
- `useBotInfoStore` — Bot 基本信息 (name, mode, icon)
- `usePersonaStore` — System Prompt
- `useModelStore` — 模型配置
- `useBotSkillStore` (315行) — 技能面板 (插件/工作流/知识库)
- `usePageRuntimeStore` (190行) — 编辑器运行时 (init, preview, saving, layout)
- `useMultiAgentStore` (574行) — 多 Agent 画布
- `useCollaborationStore` — 协作状态
- `useMonetizeStore` — 付费配置
- `useDiffStore` — Prompt diff
- `useAuditStore` — 审计
- 其余 4 个: chatBackground, onboarding, variables, memory

**Chat 区域 32 个工厂 store**: `createXxxStore(mark)` 模式，每个调试 session 独立实例

### 7. 复刻要点

**必做前端页面**: BotEditorLayout (header) + BotEditorWithContext (mode switch) + SingleMode/WorkflowMode

**必做组件** (简化版):
- BotHeader (名称+保存+发布按钮)
- PromptEditor (System Prompt 编辑)
- ModelConfigView (模型选择)
- BotDebugChatArea (聊天调试 — 可简化为基础 ChatSDK + fetchStream)

**必做接口**: `get_display_info`, `update_display_info`, `publish`, `conversation/chat`(SSE)

**必做后端**: DraftBot CRUD + Agent 对话 (agentrun SSE)

**可简化**:
- 多 Agent 画布 → 先不做
- 48 个子包 → 只做 entry + layout + prompt + chat-debug + bot-config-area (5个)
- 14 个 store → 简化为 3-5 个 (botInfo + persona + model + pageRuntime)
- 32 个 chat store → 简化为单个 messages store

**最复杂**: Provider 嵌套 (BotEditorContextProvider + BotEditorServiceProvider + PromptEditorProvider + FormilyProvider), ChatSDK + fetchStream SSE 流式

---

## 菜单 H4：工作流编辑器

### 1. 菜单定位
- **做什么**: 可视化工作流编排画布，支持 40+ 种节点的拖放连接编排
- **面向用户**: 开发者
- **业务域**: 工作流引擎
- **系统作用**: Agent 工作流技能的编辑器

### 2. 页面入口 【已确认】
- **路由**: `/work_flow?space_id=&workflow_id=`
- **入口**: `WorkflowPage` (apps/coze-studio/src/pages/workflow.tsx) → `@coze-workflow/playground-adapter` → `WorkflowPlayground`
- **Layout**: 无侧栏，独立全屏
- **参数驱动**: 完全由 URL search params 驱动

### 3. 页面结构 【已确认】

```
WorkflowPlayground (playground-adapter)
├── Toolbar (顶部: 工作流名称 + 保存 + 发布 + 测试运行)
├── Canvas Area (画布区域)
│   ├── 节点面板 (可拖拽的节点列表)
│   ├── 画布 (Fabric.js + @flowgram-adapter/free-layout-editor)
│   │   ├── Node: LLM / Plugin / Workflow / Knowledge / Database / Code / ...
│   │   └── Edge: 节点间连线
│   └── 小地图
├── Node Config Panel (右侧: 选中节点的配置面板)
└── Test Panel (底部: 测试运行输入/输出/日志)
```

### 4. 功能点清单 【已确认】

| # | 功能 | 说明 |
|---|------|------|
| 1 | **拖入节点** | 从面板拖拽节点到画布 |
| 2 | **连线** | 节点间拖拽连线 |
| 3 | **节点配置** | 点击节点 → 右侧配置面板 |
| 4 | **保存画布** | `SaveWorkflow` API |
| 5 | **测试运行** | `WorkFlowTestRun` SSE |
| 6 | **测试恢复** | `WorkFlowTestResume` (中断恢复) |
| 7 | **节点调试** | 单节点调试 `WorkflowNodeDebugV2` |
| 8 | **发布** | `PublishWorkflow` |
| 9 | **撤销/重做** | 画布操作历史 |
| 10 | **复制** | `CopyWorkflow` |
| 11 | **版本管理** | `ListPublishWorkflow` |
| 12 | **画布缩放** | 缩放 + 适应画布 |

### 5. 核心接口 【已确认】

| API | 路径 |
|-----|------|
| 创建 | POST `/api/workflow_api/create` |
| 获取画布 | POST `/api/workflow_api/canvas` |
| 保存 | POST `/api/workflow_api/save` |
| 测试运行 | POST `/api/workflow_api/test_run` (SSE) |
| 测试恢复 | POST `/api/workflow_api/test_resume` (SSE) |
| 发布 | POST `/api/workflow_api/publish` |
| 详情 | POST `/api/workflow_api/workflow_detail` |
| 节点调试 | POST `/api/workflow_api/nodeDebug` |
| 节点类型 | POST `/api/workflow_api/node_type` |

### 6. 复刻要点

**复刻复杂度**: **极高** (2200+ 文件, Inversify DI, Fabric.js)

**建议简化路线**:
1. 先用简单的 React Flow 或 LogicFlow 替代 Fabric.js + flowgram
2. 先支持 5-10 种核心节点 (开始/结束/LLM/Code/条件/Plugin)
3. 跳过 Inversify DI，用普通 React Context
4. 跳过节点注册表的 300+ 文件，手写 5-10 个节点组件
5. 画布数据用 JSON 存储，复用后端 `/api/workflow_api/*` 接口

**最复杂**: Inversify Entity 系统 + Fabric.js 画布交互 + 40+ 种节点表单配置

---

## 菜单 H5：知识库详情

### 1. 菜单定位
- **做什么**: 查看知识库文档列表、切片管理、文档上传、搜索测试
- **面向用户**: 开发者
- **业务域**: 知识库 RAG

### 2. 页面入口 【已确认】
- **路由**: `/space/:space_id/knowledge/:dataset_id`
- **入口**: `KnowledgePreviewPage` (workspace-base/pages/knowledge-preview, 96行) → 按 `biz` 参数分发四种知识库 IDE
- **上传路由**: `.../knowledge/:dataset_id/upload` → `KnowledgeUploadPage` (88行)

### 3. 功能点清单 【已确认】

| # | 功能 | API |
|---|------|-----|
| 1 | 知识库详情 | `DatasetDetail` |
| 2 | 文档列表 | `ListDocument` |
| 3 | 上传文档 | `CreateDocument` + 文件上传 |
| 4 | 删除文档 | `DeleteDocument` |
| 5 | 文档切片列表 | `ListSlice` |
| 6 | 手动添加切片 | `CreateSlice` |
| 7 | 修改切片 | `UpdateSlice` |
| 8 | 删除切片 | `DeleteSlice` |
| 9 | 重分割文档 | `Resegment` |
| 10 | 搜索测试 | `SearchDatasetDoc` |
| 11 | 启用/禁用 | `UpdateDataset` (status) |

### 4. 复刻要点

**必做接口**: Dataset CRUD + Document CRUD + Slice CRUD + 文件上传

**必做后端**: 文档解析(先支持 TXT/Markdown) + 分块 + Embedding + 向量存储(Milvus) + 全文检索(ES)

**建议简化**: 先只支持 TXT 上传 + 固定长度切片 + 单一 Embedding 模型

---

## 菜单 H7：数据库详情

### 1. 菜单定位
- **做什么**: 管理 Bot 关联的数据库表，结构编辑、数据行增删改查
- **面向用户**: 开发者

### 2. 页面入口 【已确认】
- **路由**: `/space/:space_id/database/:table_id`
- **入口**: `DatabaseDetailPage` (workspace-base/pages/database, 63行) → `@coze-data/database-v2` `DatabaseDetail`

### 3. 核心接口 【已确认】

| API | 路径 |
|-----|------|
| 列表 | POST `/api/memory/database/list` |
| 获取 | POST `/api/memory/database/get_by_id` |
| 创建 | POST `/api/memory/database/add` |
| 删除 | POST `/api/memory/database/delete` |
| 更新 | POST `/api/memory/database/update` |
| 列表数据行 | POST `/api/memory/database/list_records` |
| 更新数据行 | POST `/api/memory/database/update_records` |
| 获取表结构 | POST `/api/memory/table_schema/get` |
| 验证表结构 | POST `/api/memory/table_schema/validate` |

### 4. 复刻要点

**复刻复杂度**: 中

**必做**: 表结构编辑 + 数据行 CRUD + 绑定到 Bot

---

## 菜单 H8：插件详情

### 1. 菜单定位
- **做什么**: 编辑插件元信息、管理插件工具(API)、调试插件
- **面向用户**: 开发者

### 2. 页面入口 【已确认】
- **路由**: `/space/:space_id/plugin/:plugin_id`
- **入口**: `PluginDetailPage` (workspace-base/pages/plugin, 20行) → `@coze-agent-ide/bot-plugin/page`

### 3. 核心接口 【已确认】

| API | 路径 |
|-----|------|
| 获取插件信息 | POST `/api/plugin_api/get_plugin_info` |
| 注册插件元数据 | POST `/api/plugin_api/register_plugin_meta` |
| 更新插件 | POST `/api/plugin_api/update` |
| 创建 API | POST `/api/plugin_api/create_api` |
| 更新 API | POST `/api/plugin_api/update_api` |
| 删除 API | POST `/api/plugin_api/delete_api` |
| 调试 API | POST `/api/plugin_api/debug_api` |
| 发布插件 | POST `/api/plugin_api/publish_plugin` |
| 删除插件 | POST `/api/plugin_api/del_plugin` |

### 4. 复刻要点

**复刻复杂度**: 高

**核心**: 插件是 OpenAPI3 Schema 定义的 HTTP 工具集，核心是 API 定义编辑器 + 调试面板

---

## 菜单 2.1 + 2.2：探索商店（插件/模板）

### 1. 菜单定位
- **做什么**: 浏览公开的插件和模板，支持搜索、收藏、复制到空间
- **面向用户**: 所有登录用户
- **业务域**: 社区市场

### 2. 页面入口 【已确认】
- **路由**: `/explore/plugin`, `/explore/template`
- **入口**: `@coze-community/explore` 包 → `PluginPage` (155行), `TemplatePage` (49行)
- **搜索**: `/search/:word` → `SearchPage` (322行)

### 3. 页面结构 【已确认】

**插件商店** (`PluginPage`, 155行):
```
Layout
├── Tab 切换 (local / coze SaaS)
└── PluginPageList (无限滚动)
    └── Grid: PluginCard[]
```

**模板商店** (`TemplatePage`, 49行):
```
Layout
└── PageList (一次性加载 1000 条)
    └── Grid: TemplateCard[]
```

**搜索** (`SearchPage`, 322行):
```
Layout
├── Header (返回 + SearchInput)
├── EntityTypeSelector (Tabs: Plugin/Template/Bot)
├── Content
│   ├── SearchFilter (侧栏筛选: 分类/官方/本地/付费)
│   └── InfiniteList (搜索结果卡片)
└── Mobile: SideSheet 筛选
```

### 4. 功能点清单 【已确认】

| # | 功能 | API |
|---|------|-----|
| 1 | 插件列表 | `PublicGetProductList` |
| 2 | 模板列表 | `PublicGetProductList` |
| 3 | 搜索 | `PublicSearchProduct` |
| 4 | 搜索建议 | `PublicSearchSuggest` |
| 5 | 分类列表 | `PublicGetProductCategoryList` |
| 6 | 收藏 | `PublicFavoriteProduct` |
| 7 | 复制到空间 | `PublicDuplicateProduct` |
| 8 | 插件配置 | `PublicGetMarketPluginConfig` (SaaS tab 开关) |

### 5. 复刻要点

**复刻复杂度**: 低

**必做**: 商品列表 + 搜索 + 复制到空间

**可简化**: 先不做收藏、分类筛选，只做列表 + 搜索

---

## 菜单 H11：登录

### 1. 菜单定位
- **做什么**: 邮箱注册/登录/密码重置
- **面向用户**: 所有用户（未登录）
- **业务域**: 身份认证

### 2. 页面入口 【已确认】
- **路由**: `/sign`
- **入口**: `LoginPage` (account-ui-adapter/pages/login-page, 124行)
- **服务**: `useLoginService` (同目录 service.ts, 78行)
- **Layout**: 无侧栏，独立全屏登录页

### 3. 页面结构 【已确认】

```
SignFrame (全屏背景)
└── SignPanel (白色卡片)
    ├── Favicon (Logo)
    ├── Input: 邮箱
    ├── Input: 密码
    ├── Button: 登录 / 注册 (切换)
    ├── Link: 忘记密码
    └── Terms 链接
```

### 4. 功能点清单 【已确认】

| # | 功能 | API |
|---|------|-----|
| 1 | 邮箱登录 | POST `/api/passport/web/email/login/` |
| 2 | 邮箱注册 | POST `/api/passport/web/email/register/v2/` |
| 3 | 密码重置 | GET `/api/passport/web/email/password/reset/` |
| 4 | 获取登录态 | POST `/api/passport/account/info/v2/` |
| 5 | 退出登录 | GET `/api/passport/web/logout/` |

### 5. 前端逻辑 【已确认】

- `useLoginService` 封装 `passport.PassportWebEmailLoginPost` + `PassportWebEmailRegisterV2Post`
- 成功: `setUserInfo(data)` → `useUserStore` 更新 → `useLoginStatus()` 变为 `'logined'` → redirect `/`
- 失败: `hasError = true` → 输入框红色提示
- 表单校验: 邮箱格式 + 密码非空
- Session: 后端 `Set-Cookie: session_id=xxx` → 浏览器自动携带

### 6. 后端逻辑 【已确认】

```
POST /api/passport/web/email/login/
  → passport_service.go → UserApplicationSVC.SignIn()
  → bcrypt 密码校验 → 创建 Session → Redis 存储 → Set-Cookie
```

### 7. 复刻要点

**复刻复杂度**: 中

**必做**: 
- 前端: LoginPage + useUserStore + RequireAuthContainer
- 后端: 邮箱注册/登录 + bcrypt + Session + Redis
- 数据表: `user` (email, password_hash, name, avatar)

**必做全局机制**:
- `useCheckLogin()` → 每次进入 requireAuth 页面自动检查
- `RequireAuthContainer` → 未登录遮罩
- `axiosInstance` 401 拦截器 → redirect `/sign`

---

# 十、更新后的菜单复刻总表

| # | 菜单/页面 | 路由 | 核心功能 | 关键前端 | 关键后端 API | 复刻复杂度 | 复刻优先级 |
|---|----------|------|---------|---------|-------------|----------|----------|
| 1 | 登录 | `/sign` | 邮箱注册/登录 | `account-ui-adapter/login-page` | `/api/passport/*` | **中** | **P0** |
| 2 | App Shell | `/` | Layout+侧栏+路由 | `global-layout-composed`, `layout/sider` | — | **中** | **P0** |
| 3 | 开发列表 | `/space/:sid/develop` | Bot/Project 列表 | `workspace-adapter/develop` | `/api/intelligence_api/*` | **中** | **P0** |
| 4 | Agent IDE(简化) | `/space/:sid/bot/:bid` | Prompt+模型+聊天 | `agent-ide entry/layout/chat-debug` | `/api/draftbot/*`, `/api/conversation/*` | **高** | **P0** |
| 5 | 资源库 | `/space/:sid/library` | 5种资源管理 | `workspace-base/library` | `/api/plugin_api/library_resource_list` | **高** | **P1** |
| 6 | 知识库 | `/space/:sid/knowledge/:did` | 文档+切片管理 | `data/knowledge-*` | `/api/knowledge/*` | **高** | **P1** |
| 7 | 工作流(简化) | `/work_flow` | 5-10种节点画布 | 简化 playground | `/api/workflow_api/*` | **极高** | **P1** |
| 8 | 数据库 | `/space/:sid/database/:tid` | 表结构+数据行 | `data/database-v2` | `/api/memory/database/*` | **中** | **P2** |
| 9 | 插件详情 | `/space/:sid/plugin/:pid` | API定义+调试 | `agent-ide/bot-plugin` | `/api/plugin_api/*` | **高** | **P2** |
| 10 | 探索商店 | `/explore/*` | 插件/模板浏览 | `community/explore` | `/api/marketplace/*` | **低** | **P3** |
| 11 | 搜索 | `/search/:word` | 全局搜索 | `explore/search` | `/api/marketplace/product/search` | **低** | **P3** |

---

# 十一、最终复刻路线图

## Phase 0: 基础设施（1 周）

```
1. 后端骨架: Go + Hertz + MySQL + Redis + 中间件链 (参考 main.go)
2. 前端骨架: React 18 + TypeScript + Rsbuild + React Router 6
3. HTTP 封装: axiosInstance (双层拦截器) + fetchStream (SSE)
4. 状态管理: Zustand (per-domain 模式)
5. UI 框架: Semi Design (或其他)
6. Docker Compose: MySQL + Redis + 后端 + 前端 Nginx
```

## Phase 1: 登录 + 首页（1-2 周）

```
7. 登录/注册: LoginPage + passport API + Session + bcrypt
8. App Shell: Layout + 侧栏 + 一级菜单 (工作空间/探索)
9. 空间初始化: SpaceLayout + useInitSpace
10. 开发列表: Develop 页 + BotCard + 筛选 + 创建 Bot
11. RequireAuthContainer + useCheckLogin 全局鉴权
```

## Phase 2: Agent IDE 简化版（2-3 周）

```
12. BotEditorLayout (header + 模式切换)
13. SingleMode (配置区 + 聊天区)
14. Prompt 编辑器 (文本框即可)
15. 模型选择 (下拉选模型)
16. ChatSDK + fetchStream (SSE 流式聊天)
17. 后端: DraftBot CRUD + Agent 对话 (Eino 图构建 + SSE 推送)
18. LLM 接入: OpenAI Compatible API
```

## Phase 3: 资源管理（2-3 周）

```
19. 资源库 Library 页 (统一表格)
20. 知识库: 创建 + TXT 上传 + 分块 + Embedding + Milvus 存储
21. 插件: 创建 + API 定义 + HTTP 调用
22. 数据库: 创建 + 表结构 + 数据行
```

## Phase 4: 工作流简化版（3-4 周）

```
23. 画布: React Flow (替代 Fabric.js)
24. 5 种核心节点: 开始/结束/LLM/Code/条件
25. 后端: Eino Compose 图构建 + 节点执行
26. 测试运行 SSE
```

## Phase 5: 完善（2-3 周）

```
27. 探索商店 (列表 + 搜索 + 复制)
28. 发布系统 (Bot + Workflow + Plugin)
29. 更多工作流节点 (Plugin/Knowledge/Database/Loop/Batch)
30. 多 Agent 模式
```

---

# 十二、最小可运行复刻方案 (MVP)

## 保留的页面 (5 个)
1. `/sign` — 登录
2. `/space/:sid/develop` — 开发列表
3. `/space/:sid/bot/:bid` — Agent IDE (简化: Prompt + 模型 + 聊天)
4. `/space/:sid/library` — 资源库 (简化: 只有插件和工作流)
5. `/work_flow` — 工作流编辑器 (简化: 5 种节点)

## 保留的后端 API (12 个)
1. `POST /api/passport/web/email/login/` — 登录
2. `POST /api/passport/web/email/register/v2/` — 注册
3. `POST /api/passport/account/info/v2/` — 检查登录态
4. `POST /api/intelligence_api/search/get_draft_intelligence_list` — Bot 列表
5. `POST /api/draftbot/create` — 创建 Bot
6. `POST /api/draftbot/get_display_info` — 获取 Bot 配置
7. `POST /api/draftbot/update_display_info` — 更新 Bot 配置
8. `POST /api/conversation/chat` — Agent 对话 (SSE)
9. `POST /api/workflow_api/create` — 创建工作流
10. `POST /api/workflow_api/save` — 保存工作流
11. `POST /api/workflow_api/canvas` — 获取画布
12. `POST /api/workflow_api/test_run` — 测试运行 (SSE)

## 保留的数据表 (6 张)
1. `user` — 用户
2. `space` — 空间
3. `draft_bot` — Bot 草稿
4. `workflow` — 工作流
5. `conversation` — 对话
6. `message` — 消息

## 保留的状态流转
- Bot: 创建(草稿) → 编辑 Prompt/模型 → 调试聊天 → [可选]发布
- Workflow: 创建 → 编辑画布(5种节点) → 测试运行

## 预估工期
- 1 人全栈: **6-8 周** (不含工作流画布则 4-5 周)
- 2 人(前后端分离): **4-5 周**
