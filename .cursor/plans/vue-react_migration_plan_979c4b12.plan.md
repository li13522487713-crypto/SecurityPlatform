---
name: Vue-React Migration Plan
overview: 基于对 app-web 项目 125+ 源文件、8 个 composable、23 个 service、1 个 store、完整路由、19 个 workspace 包的深入代码分析，输出一份可直接用于研发评审和实施落地的"Vue -> React 技术栈迁移实施方案"。
todos:
  - id: phase0-api-decouple
    content: 阶段 0：api-core.ts 与 router 解耦（将 forceLogout 改为注入式回调）
    status: pending
  - id: phase0-permission-unify
    content: 阶段 0：统一权限服务（抽出 PermissionService 纯函数，消除三套并行逻辑）
    status: pending
  - id: phase0-constants-centralize
    content: 阶段 0：集中常量定义（LAST_APP_KEY_STORAGE 等）到 constants/storage-keys.ts
    status: pending
  - id: phase0-workflow-i18n-perm
    content: 阶段 0：WorkflowListPage 补全 i18n + 权限门控
    status: pending
  - id: phase0-shared-core-split
    content: 阶段 0：shared-core 拆分 framework-agnostic 子路径
    status: pending
  - id: phase1-react-scaffold
    content: 阶段 1：创建 React 项目骨架（Vite + React + TS + Zustand + TanStack Query + React Router + react-i18next + Ant Design React）
    status: pending
  - id: phase2-services-migration
    content: 阶段 2：迁移 services/types/constants（22/23 个 service 零修改搬运 + api-core 重写）
    status: pending
  - id: phase3-crud-pages
    content: 阶段 3：迁移 CRUD 页面（Positions -> Roles -> Users -> Departments）
    status: pending
  - id: phase4-special-pages
    content: 阶段 4：迁移特殊页面（Dashboard/Approval/AgentChat/DynamicTables/Workflow/Settings）
    status: pending
  - id: phase5-runtime
    content: 阶段 5：迁移低代码运行时（RuntimePageHost/DialogHost -> AMIS React SDK 原生集成）
    status: pending
  - id: phase6-cleanup
    content: 阶段 6：收尾清理（移除 Vue 依赖/veaury，全量 E2E 回归，性能对比）
    status: pending
isProject: false
---

# Atlas AppWeb Vue -> React 技术栈迁移实施方案

---

## 1. 结论摘要

**直接回答核心问题：**

- **当前 Vue 项目是否适合迁移到 React？** 适合，但**不紧迫**。项目已具备部分 React 基础（`workflow-editor-react` + `veaury` 桥接），且当前 Vue 代码质量中等偏上，Composition API 全覆盖使得逻辑迁移门槛较低。
- **是否建议迁移？** **有条件地建议**。前提是团队 React 能力成熟，且有明确的技术统一战略（消除 Vue+React 双栈维护成本）。若仅为"换技术栈"，收益不足以覆盖成本。
- **是否建议全量迁移？** **不建议一次性全量迁移**。项目涉及 19 个 workspace 包，低代码运行时 + AMIS 集成复杂度高，一次迁移风险不可控。
- **是否建议渐进式迁移？** **强烈建议渐进式迁移**。利用已有的 `veaury` 桥接能力，在 Vue 壳内逐步替换页面模块为 React。
- **当前项目最大的问题是技术栈问题还是架构问题？** **主要是架构问题**，不是技术栈问题。权限体系不统一（两套并存）、i18n 覆盖断层、`api-core` 与 router 循环耦合、shared-core 强绑 Vue 等，这些问题**换到 React 后如果不先治理，只是换了语法写了一遍相同的债务**。
- **如果直接迁移到 React，最大的风险是什么？** "换语法不换问题"——把 Vue 架构债原样搬到 React，同时承担双栈维护开销和交付停滞风险。
- **如果迁移成功，最大的收益是什么？** 消除 `veaury` 桥接层；统一技术栈降低长期维护成本；React 生态更丰富的 headless/低代码组件生态（尤其 AMIS 原生为 React）。

---

## 2. 当前项目架构识别

### 2.1 目录分层（已确认）

基于实际代码扫描（[src/frontend/apps/app-web/src/](src/frontend/apps/app-web/src/)，共 125 个源文件）：

- **pages/**: 35 个页面 .vue 文件，按业务域分 12 个子目录（system/ai/workflow/approval/settings/dynamic/dashboard/reports/visualization/runtime/app-entry/app-login）
- **components/**: 12 个组件，分 7 个子目录（ai/amis/common/dashboard/layout/system/workflow）
- **composables/**: 8 个（useAppContext/useAppCrudPage/useAudioRecorder/useDynamicTablesWorkbench/useMasterDetail/usePermission/useSelectOptions/useStreamChat）
- **stores/**: 仅 1 个 `user.ts`（Pinia store）
- **router/**: 仅 1 个 `index.ts`（集中路由表 + 全局守卫）
- **services/**: 23 个 api-*.ts 服务文件
- **runtime/**: 26 个文件的低代码运行时系统
- **types/**: 6 个类型定义文件
- **utils/**: 3 个工具文件
- **constants/**: 1 个权限常量文件
- **i18n/**: 4 个国际化文件
- **amis/**: 1 个 AMIS 环境配置文件

### 2.2 当前架构优缺点

**优点（已确认）：**
- 全量使用 `<script setup lang="ts">` + Composition API，无 Options API 残留
- CRUD 页面有统一模式：`CrudPageLayout` + 具名插槽 + `useAppCrudPage` composable
- services 层按业务域清晰拆分，纯异步函数，与 Vue 无耦合
- types 层独立，可直接复用
- 低代码运行时核心逻辑抽到了 `@atlas/runtime-core`（框架无关包）
- 工作流编辑器已是 React 实现（`@atlas/workflow-editor-react`），通过 `veaury` 桥接

**缺点/架构债务（已确认）：**
- **权限体系不统一**：`usePermission()` composable 与 `router/index.ts` 的 `checkPermission()` 存在两套并行逻辑；`AppSettingsPage.vue` 又有第三套手写判定（[src/frontend/apps/app-web/src/pages/settings/AppSettingsPage.vue](src/frontend/apps/app-web/src/pages/settings/AppSettingsPage.vue) 133-140 行）；部分页面如 WorkflowListPage **完全无权限门控**
- **i18n 覆盖断层**：WorkflowListPage 大量硬编码中文；Dashboard 混杂英文回退和原生 HTML 控件；PlaceholderPage 路由 meta 中有硬编码中文 title
- **api-core 与 router 循环耦合**：[src/frontend/apps/app-web/src/services/api-core.ts](src/frontend/apps/app-web/src/services/api-core.ts) 第 3 行直接 `import { router } from "@/router"` 用于 401 重定向，形成 `api-core <-> router` 隐式循环依赖
- **LAST_APP_KEY_STORAGE 重复定义**：在 `stores/user.ts`（第 26 行）、`router/index.ts`（第 12 行）、`api-core.ts`（第 6 行）三处重复定义同一常量
- **shared-core 强绑 Vue**：所有 composables（useCrudPage/useTableView/useMasterDetail/useSelectOptions 等）均依赖 Vue reactivity + ant-design-vue 类型，无框架无关导出
- **shared-ui 100% Vue**：12 个组件全部为 Vue SFC，无任何可复用到 React 的产出
- **页面职责不均**：部分页面（如 AppUsersPage 700+ 行、AgentChatPage 大量逻辑）过于肥胖，而 DynamicTablesPage 通过"薄视图 + 胖 composable"模式保持了良好可维护性
- **全局模块可变状态**：router 文件中 `setupChecked/platformReady/appReady/configuredAppKey/refreshTokenInflight` 为模块级可变量，难以测试和 SSR

### 2.3 关键发现：哪些问题不是 Vue 的锅

| 问题 | 根因 | 迁移 React 是否自动解决 |
|------|------|------------------------|
| 权限体系两套并存 | 架构设计缺失统一权限抽象 | 否，需先统一 |
| i18n 覆盖不完整 | 开发规范执行不一致 | 否，需补全 |
| api-core 与 router 循环耦合 | 职责边界不清 | 否，需先解耦 |
| 常量重复定义 | 模块化设计不足 | 否，需先集中 |
| 页面组件过胖 | 缺乏统一拆分规范 | 否，需先重构 |
| 全局可变状态 | Vue 没强制，但 React 也不会 | 否，需主动改进 |
| Dashboard 使用原生 HTML 控件 | 设计还原不彻底 | 否，需修复 |
| WorkflowListPage 无权限/无 i18n | 独立开发未对齐规范 | 否，需补全 |

---

## 3. Vue -> React 映射分析

### 3.1 页面组件（35 个 .vue）
- **Vue 实现**：`<script setup lang="ts">` + Composition API，使用 ref/reactive/computed/watch/onMounted
- **React 推荐**：Function Component + hooks（useState/useMemo/useCallback/useEffect）
- **迁移难度**：中（逻辑模式相似，但模板到 JSX 需手动转换）
- **风险点**：Vue 的 `watch` 与 React 的 `useEffect` 语义不完全对等；`reactive` 的深层响应在 React 中需要 immutable 更新
- **是否适合直接迁移**：CRUD 页面（Users/Roles/Departments/Positions）可直接迁移，因为有统一的 `CrudPageLayout` 模式
- **是否应该先重构后迁移**：AgentChatPage（过胖）、AppSettingsPage（权限逻辑特殊）建议先重构

### 3.2 通用组件（12 个）
- **Vue 实现**：SFC + 具名插槽（`#bodyCell`、`#extra-drawers` 等）+ v-model 双向绑定
- **React 推荐**：children/render props 替代具名插槽；受控组件替代 v-model
- **迁移难度**：中高（插槽模式到 render props/children 需重设 API）
- **风险点**：`CrudPageLayout`（在 shared-ui 中）的插槽设计深度影响所有 CRUD 页面
- **是否适合直接迁移**：StatusSwitch/StatCard 等简单组件可直接迁移
- **是否应该先重构后迁移**：AppHeader/AppSidebar（布局组件）应与布局层一起迁移

### 3.3 Composables（8 个）
- **Vue 实现**：依赖 ref/reactive/computed/watch/onMounted/onUnmounted
- **React 推荐**：custom hooks + useState/useMemo/useCallback/useEffect
- **迁移难度**：中（useDynamicTablesWorkbench 较复杂，usePermission/useAppContext 较简单）
- **风险点**：useDynamicTablesWorkbench 包含 `watch` + `isMounted` 防卸载逻辑，React 中需用 AbortController/useRef 替代
- **直接迁移**：usePermission（纯逻辑门面）可直接迁移
- **先重构**：useAppCrudPage（绑定 ant-design-vue Rule 类型）需先解耦 UI 库类型

### 3.4 Store（1 个 Pinia store）
- **Vue 实现**：`defineStore` + state/actions
- **React 推荐**：Zustand（API 最接近）或 Redux Toolkit
- **迁移难度**：低（只有 1 个 store，逻辑简单）
- **风险点**：`_getInfoInflight` 单飞模式需在 React 中用 React Query 的 `staleTime` 或手动单飞替代

### 3.5 Router（1 个文件）
- **Vue 实现**：vue-router createRouter + beforeEach 全局守卫
- **React 推荐**：React Router v6 createBrowserRouter + loader/action 或布局路由内 useEffect
- **迁移难度**：高（守卫逻辑复杂：setup 状态检测 + appKey 规范化 + token 刷新 + 权限检查）
- **风险点**：全局可变状态（setupChecked 等）迁移到 React 需改为 Context 或外部状态
- **先重构**：必须先将守卫内的业务逻辑抽成独立函数/服务，再迁移路由壳

### 3.6 Services/API（23 个文件）
- **Vue 实现**：纯 async 函数 + `requestApi`
- **React 推荐**：保持现有 fetcher 函数 + 用 TanStack Query 包装为 hooks
- **迁移难度**：**极低**（22/23 个文件与 Vue 完全无关）
- **风险点**：`api-core.ts` 第 3 行 `import { router }` 是唯一硬耦合点
- **先重构**：`api-core.ts` 必须先解耦 router 引用，改为注入 navigate 回调

### 3.7 国际化
- **Vue 实现**：vue-i18n `createI18n` + `useI18n` + `t()`
- **React 推荐**：react-i18next + `useTranslation` + `t()`
- **迁移难度**：低（词条文件 `zh-CN.ts`/`en-US.ts` 可直接复用，只需换加载器）
- **风险点**：Ant Design Vue locale 切换逻辑需改为 Ant Design React ConfigProvider

### 3.8 低代码运行时（26 个文件）
- **Vue 实现**：Pinia context store + provide/inject + Vue 宿主组件（RuntimePageHost.vue/RuntimeDialogHost.vue）
- **React 推荐**：React Context + 核心逻辑保持 `@atlas/runtime-core`（已框架无关）+ AMIS React SDK（AMIS 原生为 React）
- **迁移难度**：中高（宿主层需重写，但核心逻辑无需改动）
- **风险点**：AMIS 在 Vue 中通过自定义封装使用，迁到 React 后反而更自然（AMIS 原生 React）
- **先重构**：建议先将 `runtime-context-store.ts`（Pinia）拆为 framework-agnostic 状态 + 薄 Vue wrapper

### 3.9 AMIS 集成
- **Vue 实现**：`amis-renderer.vue` + `amis-env.ts`（自定义 fetcher/notify/router 集成）
- **React 推荐**：直接使用 AMIS 官方 React SDK `amis`（已是 React 库）
- **迁移难度**：中（fetcher/notify 逻辑可保留，router/i18n 集成需替换）
- **收益**：**这是迁移后最大的净收益点**——去掉 Vue 包装层，AMIS 原生运行

---

## 4. 不能直接平移到 React 的问题

### 4.1 必须先解决的架构问题（迁移前置条件）

1. **api-core 与 router 解耦**
   - 文件：[api-core.ts](src/frontend/apps/app-web/src/services/api-core.ts) 第 3 行、第 53-57 行
   - 问题：`forceLogout()` 直接 import router 单例，导致 service 层依赖 UI 层
   - 解法：改为 `createApiClient({ onUnauthorized: () => navigate(...) })`，在 app 入口注入

2. **权限抽象统一**
   - 涉及文件：`usePermission.ts`、`router/index.ts` 第 345-351 行、`AppSettingsPage.vue` 第 133-140 行
   - 问题：三套权限判定逻辑，短路条件不完全一致
   - 解法：抽出 `PermissionService`（纯函数），统一 `isPrivilegedUser` + `hasPermission` 语义

3. **LAST_APP_KEY_STORAGE 等常量集中**
   - 涉及文件：`stores/user.ts`、`router/index.ts`、`api-core.ts`
   - 解法：统一到 `constants/storage-keys.ts`

4. **shared-core 框架无关层拆分**
   - 当前 `@atlas/shared-core` 的 composables 全部绑定 Vue
   - 解法：拆为 `@atlas/shared-core`（纯 TS 工具/类型/API）+ `@atlas/shared-core-vue`（Vue composables）+ 未来 `@atlas/shared-core-react`（React hooks）

### 4.2 如果机械翻译会导致架构变差的地方

- **Vue 的 `watch` 不等于 `useEffect`**：Vue watch 是精确依赖追踪，React useEffect 是闭包 + deps 数组。机械翻译 `watch(X, callback)` 为 `useEffect(() => callback(), [X])` 会引入**闭包陈旧值**和**多余执行**问题
- **Vue 的 `reactive` 对象不等于 React state**：Vue reactive 允许深层修改（`form.name = 'xxx'`），React 必须 immutable 更新（`setForm({...form, name: 'xxx'})`）。机械翻译会产生大量 mutation 错误
- **Vue 的 `provide/inject` 不等于 React Context**：Vue 的 provide/inject 可以提供响应式 ref，React Context 的 value 变化会导致所有消费者重渲染，需要配合 memo/select 优化

---

## 5. React 目标架构设计

### 5.1 目标目录结构

```
apps/app-web-react/
  src/
    app/                          # 应用入口与全局配置
      App.tsx                     # 根组件
      main.tsx                    # 入口文件
      providers.tsx               # 全局 Provider 组合（QueryClient/Router/Auth/I18n/Theme）
    core/                         # 框架无关的核心逻辑
      auth/                       # 鉴权服务（纯 TS）
        auth-service.ts
        permission-service.ts     # 统一权限判定
        storage-keys.ts           # 集中常量
      api/                        # HTTP 客户端（纯 TS）
        api-client.ts             # createApiClient（不 import router）
        api-interceptors.ts       # 401/403 处理（通过事件/回调）
      config/                     # 运行时配置
    features/                     # 按业务域组织的功能模块
      system/                     # 组织管理
        pages/
          UsersPage.tsx
          RolesPage.tsx
          DepartmentsPage.tsx
          PositionsPage.tsx
        hooks/
          useUsers.ts             # TanStack Query hooks
          useRoles.ts
        services/
          org-management-api.ts   # 可从现有 api-org-management.ts 直接搬
        types/
          organization.ts         # 可从现有 types/organization.ts 直接搬
      ai/
        pages/
        hooks/
        services/
      workflow/
        pages/
        hooks/
        services/
      approval/
        pages/
        hooks/
        services/
      dashboard/
      dynamic-tables/
      settings/
      runtime/                    # 低代码运行时宿主
        pages/
          RuntimePageHost.tsx
        hooks/
          useRuntimeContext.ts
        amis/
          AmisRenderer.tsx        # AMIS 原生 React 渲染
          amis-env.ts             # 可复用现有 fetcher 逻辑
    shared/                       # 应用级共享层
      components/                 # 通用业务组件
        CrudPageLayout.tsx
        MasterDetailLayout.tsx
        StatusSwitch.tsx
        StatusBadge.tsx
      hooks/                      # 通用 hooks
        usePermission.ts
        useCrudPage.ts
        useAppContext.ts
        useStreamChat.ts
      layouts/                    # 布局组件
        AppWorkspaceLayout.tsx
        AppHeader.tsx
        AppSidebar.tsx
      i18n/                       # 国际化
        index.ts                  # react-i18next 配置
        zh-CN.ts                  # 直接复用
        en-US.ts                  # 直接复用
      router/                     # 路由配置
        index.tsx
        auth-guard.tsx            # 鉴权路由守卫（layout component）
        permission-guard.tsx      # 权限路由守卫
      stores/                     # 全局状态（Zustand）
        auth-store.ts
    types/                        # 全局类型
```

### 5.2 设计原则

- **core/ 零 UI 依赖**：auth-service、permission-service、api-client 为纯 TS，可在 Node/测试/任意框架复用
- **features/ 按业务域内聚**：每个 feature 内自包含 pages/hooks/services/types，避免跨 feature 直接引用
- **shared/ 应用级通用**：布局/通用组件/通用 hooks，跨 feature 共享
- **providers.tsx 统一注入**：QueryClientProvider + RouterProvider + AuthProvider + I18nProvider + ConfigProvider

### 5.3 如何避免把 Vue 旧问题带过去

- 权限：在 core/auth/permission-service.ts 中统一实现，所有消费方（路由守卫、组件、hooks）调用同一入口
- API 层解耦：api-client.ts 不 import router，通过 `onUnauthorized` 回调在 providers.tsx 中注入 `navigate`
- 状态管理：Zustand store 仅存最小全局态（auth），页面/feature 级状态用 React Query 或 local state
- 常量集中：所有 storage key、permission code 在 core/ 下单一定义

---

## 6. React 技术栈建议

- **框架**：React 18+（与 workflow-editor-react 已用的版本对齐）
- **类型系统**：TypeScript 5.x strict mode（沿用现有配置）
- **路由**：React Router v6（createBrowserRouter + data router）—— 因为需要 loader/action 模式处理守卫逻辑
- **状态管理**：Zustand（API 最接近 Pinia，学习成本低，体积小）—— 仅用于 auth 等全局态
- **服务端状态**：TanStack Query v5（自动缓存、去重、轮询、乐观更新，可替代现有大量手动 fetch+loading+error 管理）
- **UI 组件库**：Ant Design React 5.x（与现有 ant-design-vue 组件对应度最高，降低 UI 还原成本）
- **表单**：Ant Design Form（与 UI 库一体）+ 可选 react-hook-form（复杂场景）
- **样式**：CSS Modules（与现有 scoped style 语义对应）或 Tailwind CSS（如团队偏好）
- **国际化**：react-i18next（API 与 vue-i18n 极相似：`useTranslation` vs `useI18n`，`t()` 相同）
- **图表**：Apache ECharts for React（替代 vue-echarts）
- **测试**：Vitest + React Testing Library + Playwright（沿用现有 E2E 体系）
- **构建**：Vite 8+（现有配置已同时启用 `@vitejs/plugin-vue` 和 `@vitejs/plugin-react`，可平滑过渡）
- **低代码**：AMIS React SDK（去掉 Vue wrapper，直接原生集成）

---

## 7. 完整迁移实施方案

### 阶段 0：迁移前重构（预计 1-2 周）

**目标**：解决架构债务，让迁移不搬旧问题

- [ ] **P0-1**：api-core 与 router 解耦。将 `forceLogout` 改为注入式回调
- [ ] **P0-2**：权限服务统一。抽出 `PermissionService`，路由/页面/composable 统一调用
- [ ] **P0-3**：常量集中到 `constants/storage-keys.ts`
- [ ] **P0-4**：WorkflowListPage 补全 i18n + 权限门控
- [ ] **P0-5**：shared-core 拆分 framework-agnostic 子路径

### 阶段 1：基础设施搭建（预计 1 周）

- [ ] **P1-1**：创建 `apps/app-web-react/` 项目骨架（Vite + React + TS）
- [ ] **P1-2**：配置 Zustand auth store
- [ ] **P1-3**：配置 React Router（路由表 + 鉴权守卫布局组件）
- [ ] **P1-4**：配置 TanStack Query
- [ ] **P1-5**：配置 react-i18next（直接导入现有 zh-CN.ts / en-US.ts）
- [ ] **P1-6**：配置 Ant Design React ConfigProvider + 主题
- [ ] **P1-7**：搭建 AppWorkspaceLayout（Header + Sidebar + Outlet）

### 阶段 2：Services/Types/Constants 迁移（预计 2-3 天）

**这是投入产出比最高的阶段**——22/23 个 service 文件可直接搬运。

- [ ] **P2-1**：搬运所有 `types/*.ts` 和 `constants/*.ts`（零修改）
- [ ] **P2-2**：搬运所有 `services/api-*.ts`（除 api-core 外零修改）
- [ ] **P2-3**：重写 `api-core.ts`（去掉 router import，改为回调注入）
- [ ] **P2-4**：为每个 service 创建对应的 TanStack Query hooks（`useUsers`、`useRoles` 等）

### 阶段 3：CRUD 页面迁移（预计 2-3 周）

按"最简 -> 复杂"的顺序：

- [ ] **P3-1**：迁移 `CrudPageLayout` 共享组件（从 shared-ui 的 Vue SFC 重写为 React）
- [ ] **P3-2**：迁移 AppPositionsPage（最简单的 CRUD）
- [ ] **P3-3**：迁移 AppRolesPage（CRUD + 二级抽屉）
- [ ] **P3-4**：迁移 AppUsersPage（CRUD + 树选择 + 多表单模式）
- [ ] **P3-5**：迁移 AppDepartmentsPage（树表 + 受控展开）

### 阶段 4：特殊页面迁移（预计 2-3 周）

- [ ] **P4-1**：迁移 AppDashboardPage（图表 + 多数据源聚合）
- [ ] **P4-2**：迁移 ApprovalWorkspacePage（多 Tab + 共享分页）
- [ ] **P4-3**：迁移 AgentChatPage（流式聊天 + 录音 + 附件）
- [ ] **P4-4**：迁移 DynamicTablesPage + composable（"薄视图+胖 hook"模式）
- [ ] **P4-5**：迁移 WorkflowListPage + WorkflowEditorPage（去掉 veaury，直接引用 React 编辑器）
- [ ] **P4-6**：迁移 AppSettingsPage + 子 Tab

### 阶段 5：低代码运行时迁移（预计 2-3 周）

- [ ] **P5-1**：重写 RuntimePageHost / RuntimeDialogHost 为 React（使用 AMIS React SDK）
- [ ] **P5-2**：将 runtime-context-store 从 Pinia 改为 Zustand 或 React Context
- [ ] **P5-3**：迁移 amis-env.ts（替换 vue-router/ant-design-vue 为 react-router/antd）
- [ ] **P5-4**：迁移 action-executor 中的宿主能力注入
- [ ] **P5-5**：迁移 bootstrap-runtime.ts

### 阶段 6：收尾与清理（预计 1 周）

- [ ] **P6-1**：移除 veaury 依赖
- [ ] **P6-2**：移除 Vue 相关依赖（vue/vue-router/vue-i18n/ant-design-vue/pinia 等）
- [ ] **P6-3**：更新 vite.config.ts（移除 vue plugin）
- [ ] **P6-4**：全量 E2E 回归测试
- [ ] **P6-5**：性能基线对比

---

## 8. 小闭环迁移 Case

### Case 1：AppPositionsPage — 最简 CRUD 列表页

**当前 Vue 涉及文件（已确认）：**
- [src/frontend/apps/app-web/src/pages/system/AppPositionsPage.vue](src/frontend/apps/app-web/src/pages/system/AppPositionsPage.vue) — 页面
- [src/frontend/apps/app-web/src/composables/useAppCrudPage.ts](src/frontend/apps/app-web/src/composables/useAppCrudPage.ts) — CRUD composable
- [src/frontend/apps/app-web/src/services/api-org-management.ts](src/frontend/apps/app-web/src/services/api-org-management.ts) — API 服务
- [src/frontend/packages/shared-ui/src/CrudPageLayout.vue](src/frontend/packages/shared-ui/) — 共享 CRUD 布局
- [src/frontend/packages/shared-core/src/composables/useCrudPage.ts](src/frontend/packages/shared-core/) — 共享 CRUD 逻辑

**React 中需要新建的文件：**
- `features/system/pages/PositionsPage.tsx`
- `shared/components/CrudPageLayout.tsx`（首次需重写）
- `shared/hooks/useCrudPage.ts`
- `features/system/hooks/usePositions.ts`（TanStack Query wrapper）
- `features/system/services/org-management-api.ts`（直接搬运）

**哪些逻辑直接迁：**
- service 层 API 调用函数（零修改）
- types/permissions 常量（零修改）
- i18n 词条（零修改）
- 列定义逻辑（computed -> useMemo）
- 分页参数管理（reactive -> useState）

**哪些逻辑要重构：**
- `useAppCrudPage` 中的 Ant Design Vue `Rule[]` 类型 -> Ant Design React `Rule[]`
- `CrudPageLayout` 的 Vue 插槽 API -> React children/render props
- `v-model:keyword` -> controlled input `value` + `onChange`

**验证标准：**
1. 列表加载、分页切换、搜索功能正常
2. 新增/编辑表单弹窗（Drawer）打开、提交、关闭流程正常
3. 删除确认弹窗（Popconfirm）正常
4. 权限控制（创建/编辑/删除按钮显隐）与 Vue 版一致
5. i18n 中英文切换正常
6. API 请求参数与 Vue 版完全一致（用 DevTools Network 对比）

**闭环标准：** 用同一套 `.http` 测试文件验证后端接口兼容性；前端 E2E 覆盖列表/新增/编辑/删除全流程。

---

### Case 2：ApprovalWorkspacePage — 多 Tab 表格 + 表单弹窗

**当前 Vue 涉及文件（已确认）：**
- [src/frontend/apps/app-web/src/pages/approval/ApprovalWorkspacePage.vue](src/frontend/apps/app-web/src/pages/approval/ApprovalWorkspacePage.vue)
- [src/frontend/apps/app-web/src/pages/approval/ApprovalInstanceDetailPage.vue](src/frontend/apps/app-web/src/pages/approval/ApprovalInstanceDetailPage.vue)
- [src/frontend/apps/app-web/src/services/api-approval.ts](src/frontend/apps/app-web/src/services/api-approval.ts)

**React 中需要新建的文件：**
- `features/approval/pages/ApprovalWorkspacePage.tsx`
- `features/approval/pages/ApprovalInstanceDetailPage.tsx`
- `features/approval/hooks/useApprovalList.ts`（4 个 tab 对应 4 个 query key）
- `features/approval/services/approval-api.ts`（直接搬运）

**直接迁移的逻辑：** service 层全部、类型定义、i18n 词条、状态/审批映射函数
**需要重构的逻辑：** 四 Tab 共享 `pagination`（React 中改为每个 tab 独立 query state by TanStack Query）；Tab 切换时的 URL 同步（Vue 版未完整实现 -> 在 React 中用 `useSearchParams` 完善）

**验证标准：** 4 个 Tab 独立加载/分页；审批详情页跳转正常；状态标签颜色与 Vue 版一致。

---

### Case 3：AppDepartmentsPage — 树 + 详情联动页

**当前 Vue 涉及文件（已确认）：**
- [src/frontend/apps/app-web/src/pages/system/AppDepartmentsPage.vue](src/frontend/apps/app-web/src/pages/system/AppDepartmentsPage.vue)
- [src/frontend/apps/app-web/src/services/api-org-management.ts](src/frontend/apps/app-web/src/services/api-org-management.ts)（departments 相关接口）

**React 中需要新建的文件：**
- `features/system/pages/DepartmentsPage.tsx`
- `features/system/hooks/useDepartments.ts`
- `features/system/hooks/useDepartmentTree.ts`（树构建 + 搜索过滤）

**关键技术映射：**
- Vue `computed` 构建树 + 搜索过滤 -> React `useMemo` + 纯函数 `buildTree()`
- Vue `watch(displayTree)` 修剪 expandedRowKeys -> React `useEffect` + `setExpandedRowKeys`
- Vue `a-table` + `children-column-name` + `expandable` -> Ant Design React Table + `expandable` prop
- Vue `debounce` 父部门搜索 -> `useDeferredValue` 或自定义 `useDebounce` hook

**验证标准：** 树展开/折叠行为一致；搜索过滤实时响应；新增/编辑子部门表单中父部门选择器正常；`localeCompare` 排序与 Vue 版一致。

---

## 9. 风险清单

- **架构风险**：若跳过"阶段 0 重构"直接迁移，权限不统一、api-router 循环耦合等问题会原样复制到 React，导致"换语法不换问题"
- **平台层缺失风险**：`@atlas/shared-core` 和 `@atlas/shared-ui` 是 100% Vue，React 版必须重建，但初期可能功能不完整导致页面体验降级
- **状态管理风险**：Vue 的 `reactive` 深层响应与 React 的 immutable 更新模型差异大，机械翻译会引入大量 bug（表单编辑场景尤其明显）
- **UI 一致性风险**：Ant Design Vue 4.x 与 Ant Design React 5.x 在组件 API、默认样式上有差异（如 Table column render vs bodyCell slot），需逐组件验证
- **组件复用风险**：`CrudPageLayout` 的 Vue 插槽设计需完全重写为 React 模式（render props/compound components），是迁移中最高频依赖的组件
- **路由切换风险**：vue-router 的 `beforeEach` 守卫逻辑复杂（5 个模块级可变状态），迁到 React Router 需要重新设计鉴权流程
- **权限风险**：三套权限逻辑合并时可能改变部分页面的访问行为，需要回归测试
- **性能风险**：React 缺乏 Vue 的精确依赖追踪，大表格/大列表场景可能需要额外 memo 优化
- **测试风险**：现有 Playwright E2E 测试绑定了 Vue 组件的 DOM 结构，迁移后 DOM 变化可能导致大量 E2E 失败需修复
- **发布风险**：渐进式迁移期间 Vue+React 双栈并存，bundle 体积增大（目前已有 React 18 在依赖中，影响有限）
- **团队协作风险**：迁移期间部分代码在 Vue 壳内、部分在 React 壳内，团队需要同时维护两套代码规范和 review 标准
- **双栈维护风险**：渐进式迁移周期越长，双栈维护成本越高；建议设定 12 周硬截止期限

---

## 10. 最终建议

- **是否值得从 Vue 改到 React？** 中期值得（6-12 个月视角），但**不应该是当前最高优先级**。建议先完成阶段 0 的架构重构，这些改进在 Vue 下也有巨大价值。
- **最合理的迁移方式是什么？** **渐进式迁移**：保持 Vue 壳，利用 veaury 或微前端机制逐模块替换为 React。优先迁移与 React 生态天然对齐的模块（工作流编辑器已是 React、AMIS 原生 React）。
- **最需要先重构的部分是什么？** `api-core.ts` 的 router 解耦 + 权限服务统一 + shared-core framework-agnostic 层拆分。
- **最不应该直接照搬的部分是什么？** `router/index.ts` 的守卫逻辑（5 个全局可变状态 + 复杂条件分支）——应重新设计为 React 的声明式路由守卫模式。`stores/user.ts` 中的 `getInfo` 单飞逻辑——在 React 中应改用 TanStack Query 的内置去重能力。
- **最推荐的 React 目标架构是什么？** Feature-based 分层（core/features/shared），core 层零 UI 依赖，features 按业务域内聚，TanStack Query 管理服务端状态，Zustand 仅管理最小全局状态。
- **应该先从哪个小闭环 case 开始验证？** **Case 1（AppPositionsPage）**——它是最简 CRUD 页面，涉及文件最少，但能验证 CrudPageLayout 重写、TanStack Query hooks、Ant Design React Table、权限控制、i18n 等关键基础设施全链路。一旦 Case 1 闭环成功，后续 CRUD 页面可批量迁移。
