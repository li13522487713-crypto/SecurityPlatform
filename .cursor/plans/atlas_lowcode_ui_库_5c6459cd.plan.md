---
name: Atlas LowCode UI 库
overview: 在 src/frontend 下新建独立的 Vue 3 组件库项目 Atlas.LowCodeUI，以 Vite lib 模式封装 amis + amis-editor，对外提供可视化低代码设计器画布及渲染器 Vue 组件，可被 Atlas.WebApp 及其他项目作为 npm 包引用。
todos:
  - id: scaffold
    content: "【基础】创建 Atlas.LowCodeUI 项目目录：package.json（@atlas/lowcode-ui）、tsconfig.json、vite.config.ts（lib 模式 ESM+UMD，external: vue/amis/amis-editor/react/react-dom）"
    status: pending
  - id: amis-env
    content: 【基础】实现 useAmisEnv composable：构造 AMIS env 对象（fetcher、notify/toast、alert、confirm、theme、locale、copy），作为所有组件的底层依赖
    status: pending
  - id: amis-renderer
    content: 【基础】实现 AmisRenderer 组件：封装 amis.render()，Props = schema/data/theme/locale/env，Emits = onAction/onFetch，挂载/卸载时管理 React 实例
    status: pending
  - id: page-preview
    content: 【基础】实现 PagePreview 组件：全屏只读渲染模式，复用 AmisRenderer，提供 open/close 方法和 ESC 关闭快捷键
    status: pending
  - id: form-base-controls
    content: 【表单 II-2.2 基础控件】AmisRenderer 透传验证：涵盖 input-text/password/number/email/url/textarea/editor/input-color/input-rating/input-range/input-tag 共 11 类控件的 Schema props 透传与渲染验证
    status: pending
  - id: form-select-controls
    content: 【表单 II-2.2 选择控件】select/multi-select/checkboxes/radios/list-select/button-group-select/transfer/input-tree 共 8 类选择控件渲染验证，含远程 source 场景
    status: pending
  - id: form-date-upload-controls
    content: 【表单 II-2.2 日期/上传控件】input-date/datetime/date-range/time/input-file/input-image/input-city/combo/input-table/condition-builder 渲染验证
    status: pending
  - id: form-layout-modes
    content: 【表单 II-2.3 布局模式】封装 FormLayoutMode 类型（default/horizontal/inline），AmisRenderer 支持通过 Props 注入 mode，提供三种布局示例 Schema
    status: pending
  - id: form-validation
    content: 【表单 II-2.4 验证规则】封装 FormValidationPreset 工具函数：必填/邮箱/URL/IP/手机/正则/minLength/maxLength/minimum/maximum/多字段联合校验（rules），导出可复用校验片段
    status: pending
  - id: data-crud
    content: 【数据展示 III-3.3 CRUD】AmisRenderer 支持 crud 组件全特性 Schema：sortable/filterable/分页/批量操作/列显隐/行内编辑 quickEdit/导出 Excel/headerToolbar+footerToolbar
    status: pending
  - id: data-table-list-cards
    content: 【数据展示 III-3.2 列表类】table/list/cards 三类展示组件 Schema 示例封装，提供 AtlasTableSchema/AtlasListSchema/AtlasCardsSchema 预置 Schema 工厂函数
    status: pending
  - id: data-chart
    content: 【数据展示 III-3.2 图表】chart 组件（ECharts）封装：柱状图/折线图/饼图/散点图预置 Schema 工厂，支持 api 数据源与静态数据两种模式
    status: pending
  - id: data-stat-misc
    content: 【数据展示 III-3.2 统计值】stat/timeline/progress/tag/calendar 组件 Schema 透传验证，提供 DashboardPanel 组合 Schema（stat + chart 栅格）
    status: pending
  - id: layout-page
    content: 【布局 IV-4.3 Page】封装 PageSchemaBuilder：支持 body/aside/toolbar 区域划分、initApi/interval 轮询、pullRefresh、asideResizor、cssVars 主题变量注入
    status: pending
  - id: layout-grid-flex
    content: 【布局 IV-4.2 栅格】封装 GridSchemaBuilder/FlexSchemaBuilder：12 栏响应式栅格（md/sm/xs），提供仪表盘 3/4 列示例 Schema
    status: pending
  - id: layout-panel-tabs-collapse
    content: 【布局 IV-4.2 面板/标签页/折叠】封装 PanelSchemaBuilder/TabsSchemaBuilder/CollapseSchemaBuilder，支持 Tab 懒加载与动态 title
    status: pending
  - id: layout-wizard
    content: 【布局 IV-4.2 向导】封装 WizardSchemaBuilder：多步骤表单向导，支持步骤数组、每步独立验证、步骤完成后 api 提交
    status: pending
  - id: layout-dialog-drawer
    content: 【布局 IV-4.2 弹窗/抽屉】封装 DialogSchemaBuilder/DrawerSchemaBuilder：支持 size/closeOnEsc/actions，与 AmisRenderer 联动触发
    status: pending
  - id: action-ajax-url
    content: 【动作 V-5.3 Ajax/跳转】封装 ActionBuilder 工具：ajax（含 messages.success/failed）、url/link 页面跳转、toast/confirm/copy/print/download 共 7 类动作 Schema 片段
    status: pending
  - id: action-dialog-drawer-reload
    content: 【动作 V-5.3 弹窗/刷新/提交】actionType = dialog/drawer/reload/submit/reset/setValue 共 6 类动作 Schema 片段，支持 reload 指定 componentId 刷新列表
    status: pending
  - id: action-broadcast
    content: 【动作 V-5.2 广播事件】封装 BroadcastActionBuilder：broadcast 派发 + onEvent 监听跨组件通信模式，提供日期联动图表刷新完整示例 Schema
    status: pending
  - id: action-condition
    content: 【动作 V-5.4 条件控制】封装 ConditionExprBuilder：visibleOn/disabledOn/requiredOn/hiddenOn 表达式工具函数，支持权限角色与数据值联合判断
    status: pending
  - id: datasource-api-config
    content: 【数据源 VI-6.2 API 配置】封装 ApiConfigBuilder：字符串形式（GET /api/xxx）与对象形式（method/url/headers/data/responseData）两种，支持 Bearer Token 动态注入
    status: pending
  - id: datasource-data-chain
    content: 【数据源 VI-6.1 数据域/数据链】在 useAmisEnv 中补充数据域文档与 initData 注入接口；封装 DataChainHelper：说明 Page→CRUD→Form 数据链继承机制，提供父子数据域示例 Schema
    status: pending
  - id: datasource-mapping-filter
    content: 【数据源 VI-6.3 数据映射/过滤器】封装 DataMappingHelper：${var}、${obj.field}、${arr[0]}、${val|upperCase}、${date|date:'YYYY-MM-DD'}、${price|number:2} 常用过滤器示例集
    status: pending
  - id: datasource-adapter
    content: 【数据源 VI-6.4 接口适配】封装 ResponseAdapterBuilder：将非标准接口 responseData 映射为 AMIS 规范格式（items/total/hasMore），提供 legacy API 适配示例
    status: pending
  - id: advanced-custom-sdk
    content: 【高级 VII-7.1 自定义组件 SDK 方式】实现 registerCustomSdkComponent 函数：通过 custom 类型注册带 onMount/onUpdate/onUnmount 钩子的自定义组件，支持 Vue/jQuery DOM 操作
    status: pending
  - id: advanced-custom-react
    content: 【高级 VII-7.1 自定义组件 React 方式】实现 registerReactComponent 函数：将 React 组件注册进 AMIS 组件面板，提供 amis-ui 纯 UI 库混合模式示例
    status: pending
  - id: advanced-theme
    content: 【高级 VII-7.2 主题定制】封装 ThemeProvider 组件：支持三层主题定制（cssVars 动态变量 / 辅助 Class / 全局 CSS 变量覆盖），提供暗色/浅色主题切换示例
    status: pending
  - id: advanced-i18n
    content: 【高级 VII-7.3 国际化】封装 useAmisLocale composable：与宿主 Vue I18n 的 locale 联动，支持 zh-CN/en-US 动态切换并传入 amis.embed locale 参数
    status: pending
  - id: advanced-permission
    content: 【高级 VII-7.4 权限控制】封装 PermissionExprHelper：基于 RBAC permissions 数组的 visibleOn/disabledOn 表达式生成函数，与后端 JWT claims 对接示例
    status: pending
  - id: advanced-perf
    content: 【高级 VII-7.5 性能优化】在 AmisRenderer 上暴露 lazyLoad/debug props；封装 ApiCacheConfig 工具（接口缓存时间）；提供虚拟滚动大列表 Schema 最佳实践文档片段
    status: pending
  - id: advanced-editor
    content: 【高级 VII-7.7 amis-editor 画布】实现 AmisDesigner 组件：三栏布局（组件面板240px/画布自适应/属性面板320px），顶部工具栏（保存/预览/撤销/重做/导入导出JSON），封装 amis-editor，v-model 双向绑定 Schema
    status: pending
  - id: schema-history
    content: 【收尾】实现 useSchemaHistory composable：Schema 历史栈（撤销/重做，默认栈深 50，可配置）
    status: pending
  - id: schema-json-editor
    content: 【收尾】实现 SchemaJsonEditor 组件：Monaco Editor 嵌入，实时 JSON 校验，与 AmisDesigner 双向同步 Schema
    status: pending
  - id: styles
    content: 【收尾】整理 amis-theme.css（CSS 变量覆盖）与 designer.css（三栏布局），确保样式隔离不污染宿主
    status: pending
  - id: index-export
    content: 【收尾】完善 src/index.ts：统一导出所有组件/composables/工具函数，vite-plugin-dts 生成完整 .d.ts 类型声明
    status: pending
  - id: integrate-webapp
    content: 【收尾】Atlas.WebApp 通过 file:../Atlas.LowCodeUI 引用库，更新 src/pages/lowcode/ 与 src/components/amis/ 使用库组件，验证构建通过
    status: pending
isProject: false
---

# Atlas.LowCodeUI — AMIS 可视化低代码组件库

## 项目命名

- **目录名**：`src/frontend/Atlas.LowCodeUI`
- **npm package name**：`@atlas/lowcode-ui`
- **命名理由**：与现有 `Atlas.WebApp` 风格一致；`LowCode` 点明职责；`UI` 表示对外暴露的是 Vue 可复用组件。

## 定位与边界


| 职责  | 说明                                             |
| --- | ---------------------------------------------- |
| 渲染层 | 把 AMIS JSON Schema 渲染成页面（`<AmisRenderer>`）     |
| 设计层 | 封装 amis-editor 画布提供拖拽低代码编辑能力（`<AmisDesigner>`） |
| 扩展层 | 支持注册自定义 Vue/React 组件插入 AMIS 组件面板               |
| 不包含 | 业务逻辑、后端 API、权限路由（这些留给 Atlas.WebApp）            |


## 技术选型

- **构建**：Vite 8 `build.lib` 模式，输出 ESM + UMD 两份产物
- **运行时**：Vue 3（peer dependency，不打包进库）
- **核心依赖**：`amis` ^6（渲染引擎）、`amis-editor` ^3（可视化画布）
- **类型**：TypeScript 5，strict 模式，自动 `d.ts` 输出
- **样式**：只导出 CSS 文件，不做全局污染；提供主题覆盖变量

## 项目结构

```
src/frontend/Atlas.LowCodeUI/
├── package.json               # name: @atlas/lowcode-ui
├── vite.config.ts             # lib mode: entry=src/index.ts
├── tsconfig.json
├── src/
│   ├── index.ts               # 统一导出 + Vue plugin install()
│   ├── components/
│   │   ├── AmisRenderer/      # amis.render() 包装成 Vue 组件
│   │   │   ├── index.vue
│   │   │   └── types.ts
│   │   ├── AmisDesigner/      # amis-editor 画布（左:组件面板/中:画布/右:属性面板）
│   │   │   ├── index.vue
│   │   │   ├── toolbar.vue    # 顶部工具栏（预览/保存/撤销/重做）
│   │   │   └── types.ts
│   │   ├── SchemaJsonEditor/  # JSON 代码编辑器（Monaco/CodeMirror）
│   │   │   └── index.vue
│   │   └── PagePreview/       # 全屏预览模式（只读渲染）
│   │       └── index.vue
│   ├── composables/
│   │   ├── useAmisEnv.ts      # 构造 AMIS env(fetcher/theme/locale/toast)
│   │   └── useSchemaHistory.ts # 撤销/重做 Schema 历史栈
│   ├── plugins/
│   │   └── registerCustom.ts  # 注册自定义 AMIS 组件的辅助函数
│   ├── styles/
│   │   ├── amis-theme.css     # AMIS 主题变量覆盖
│   │   └── designer.css       # 画布布局样式
│   └── types/
│       └── amis-shim.d.ts     # amis/amis-editor 类型补丁
└── dist/                      # 构建产物（.gitignore）
    ├── atlas-lowcode-ui.es.js
    ├── atlas-lowcode-ui.umd.js
    └── style.css
```

## 核心组件设计

### AmisRenderer（渲染器）

- Props：`schema: AmisSchema`、`data?: Record<string,unknown>`、`theme?: string`、`locale?: string`
- 内部调用 `amis.render(schema, { env })` 并挂载到容器 div
- Emits：`onAction`、`onFetch`（可由宿主应用注入自定义 fetcher）

### AmisDesigner（可视化画布）

- 画布布局参考 amis-editor 官方三栏结构：

```
  ┌──────────────────────────────────────────────────┐
  │  Toolbar（顶部：保存/预览/撤销/重做/导入/导出JSON）│
  ├──────────┬──────────────────────┬────────────────┤
  │ 组件面板  │     画 布 区 域       │  属性配置面板  │
  │（左 240px）│   （中，可拖拽）     │  （右 320px）  │
  └──────────┴──────────────────────┴────────────────┘
  

```

- Props：`modelValue: AmisSchema`（v-model 双向绑定）、`customComponents?: CustomComponentDef[]`
- Emits：`update:modelValue`、`onSave`
- 内部用 `useSchemaHistory` 提供撤销/重做
- `amis-editor` 在 vite.config.ts 中设为 external，由宿主项目提供（避免重复打包）

### 插件注册方式（宿主项目使用）

```typescript
// 方式一：全局注册（Atlas.WebApp main.ts）
import AtlasLowCodeUI from '@atlas/lowcode-ui'
import '@atlas/lowcode-ui/dist/style.css'
app.use(AtlasLowCodeUI)

// 方式二：按需引入
import { AmisRenderer, AmisDesigner } from '@atlas/lowcode-ui'
```

## Vite lib 模式配置要点

```typescript
// vite.config.ts 关键部分
build: {
  lib: {
    entry: 'src/index.ts',
    name: 'AtlasLowCodeUI',
    fileName: (format) => `atlas-lowcode-ui.${format}.js`,
  },
  rollupOptions: {
    external: ['vue', 'amis', 'amis-editor', 'react', 'react-dom'],
    output: {
      globals: { vue: 'Vue', amis: 'amis', react: 'React', 'react-dom': 'ReactDOM' }
    }
  }
}
```

## 与 Atlas.WebApp 的集成方式

在 `Atlas.WebApp/package.json` 中用本地路径引用：

```json
"dependencies": {
  "@atlas/lowcode-ui": "file:../Atlas.LowCodeUI"
}
```

现有 `src/pages/lowcode/` 和 `src/components/amis/` 中的页面可逐步迁移为使用库组件。

## 任务全景（按 AMIS 章节映射）

```mermaid
flowchart TD
    scaffold["基础脚手架\n(scaffold + amis-env\n+ amis-renderer + page-preview)"]

    subgraph formGroup [二 表单组件体系]
        form1["基础控件 11 类\n(form-base-controls)"]
        form2["选择控件 8 类\n(form-select-controls)"]
        form3["日期/上传控件\n(form-date-upload-controls)"]
        form4["3 种布局模式\n(form-layout-modes)"]
        form5["验证规则封装\n(form-validation)"]
    end

    subgraph dataGroup [三 数据展示组件]
        data1["CRUD 全特性\n(data-crud)"]
        data2["Table/List/Cards\n(data-table-list-cards)"]
        data3["Chart ECharts\n(data-chart)"]
        data4["Stat/Timeline/Progress\n(data-stat-misc)"]
    end

    subgraph layoutGroup [四 布局容器]
        lay1["Page 顶级容器\n(layout-page)"]
        lay2["Grid/Flex 栅格\n(layout-grid-flex)"]
        lay3["Panel/Tabs/Collapse\n(layout-panel-tabs-collapse)"]
        lay4["Wizard 多步向导\n(layout-wizard)"]
        lay5["Dialog/Drawer\n(layout-dialog-drawer)"]
    end

    subgraph actionGroup [五 动作与交互]
        act1["Ajax/跳转/Toast 7类\n(action-ajax-url)"]
        act2["Dialog/Reload/setValue\n(action-dialog-drawer-reload)"]
        act3["广播事件\n(action-broadcast)"]
        act4["条件表达式\n(action-condition)"]
    end

    subgraph dsGroup [六 数据源与API]
        ds1["API 配置构建器\n(datasource-api-config)"]
        ds2["数据域/数据链\n(datasource-data-chain)"]
        ds3["数据映射过滤器\n(datasource-mapping-filter)"]
        ds4["接口适配 Adapter\n(datasource-adapter)"]
    end

    subgraph advGroup [七 高级功能]
        adv1["自定义组件 SDK\n(advanced-custom-sdk)"]
        adv2["自定义组件 React\n(advanced-custom-react)"]
        adv3["主题定制 ThemeProvider\n(advanced-theme)"]
        adv4["国际化 useAmisLocale\n(advanced-i18n)"]
        adv5["权限控制表达式\n(advanced-permission)"]
        adv6["性能优化配置\n(advanced-perf)"]
        adv7["amis-editor 画布\n(advanced-editor)"]
    end

    subgraph finishGroup [收尾整合]
        fin1["useSchemaHistory\n(schema-history)"]
        fin2["SchemaJsonEditor\n(schema-json-editor)"]
        fin3["样式隔离\n(styles)"]
        fin4["统一导出 index.ts\n(index-export)"]
        fin5["Atlas.WebApp 集成\n(integrate-webapp)"]
    end

    scaffold --> formGroup
    scaffold --> dataGroup
    scaffold --> layoutGroup
    scaffold --> actionGroup
    scaffold --> dsGroup
    advGroup --> finishGroup
    formGroup --> finishGroup
    dataGroup --> finishGroup
    layoutGroup --> finishGroup
    actionGroup --> finishGroup
    dsGroup --> finishGroup
```



### 各章节任务说明

**二、表单组件体系（5 tasks）**

- `form-base-controls`：11 类文本/数值/富文本控件透传渲染验证
- `form-select-controls`：8 类选择类控件 + 远程 source 场景
- `form-date-upload-controls`：日期/上传/城市/combo/condition-builder 等
- `form-layout-modes`：default / horizontal / inline 三种布局 Props 封装
- `form-validation`：必填/格式/长度/范围/正则/多字段 rules 联合校验工具函数

**三、数据展示组件（4 tasks）**

- `data-crud`：sortable/filterable/分页/批量/quickEdit/导出 Excel
- `data-table-list-cards`：Schema 工厂函数 AtlasTableSchema / AtlasListSchema / AtlasCardsSchema
- `data-chart`：柱/线/饼/散点图预置 Schema，支持 api 数据源
- `data-stat-misc`：stat/timeline/progress/tag 及 DashboardPanel 组合

**四、布局容器组件（5 tasks）**

- `layout-page`：侧边栏/工具栏/initApi 轮询/cssVars
- `layout-grid-flex`：12 栏响应式栅格 Builder
- `layout-panel-tabs-collapse`：Tab 懒加载/动态 title
- `layout-wizard`：步骤数组/独立验证/步骤提交 api
- `layout-dialog-drawer`：size/closeOnEsc/联动触发

**五、动作与交互系统（4 tasks）**

- `action-ajax-url`：ajax/url/link/toast/confirm/copy/print/download 7 类 ActionBuilder
- `action-dialog-drawer-reload`：dialog/drawer/reload/submit/reset/setValue 6 类
- `action-broadcast`：broadcast 派发 + onEvent 监听，日期→图表跨组件联动示例
- `action-condition`：visibleOn/disabledOn/requiredOn/hiddenOn 表达式工具函数

**六、数据源与 API 集成（4 tasks）**

- `datasource-api-config`：字符串/对象两种 API 配置，Bearer Token 动态注入
- `datasource-data-chain`：Page→CRUD→Form 数据链继承机制说明 + initData 注入
- `datasource-mapping-filter`：6 种常用过滤器示例集
- `datasource-adapter`：responseData 非标准接口 Adapter 封装

**七、高级功能（7 tasks）**

- `advanced-custom-sdk`：onMount/onUpdate/onUnmount 钩子自定义组件注册
- `advanced-custom-react`：React 组件注册进 AMIS 组件面板
- `advanced-theme`：cssVars / 辅助 Class / 全局变量三层主题，暗/亮切换
- `advanced-i18n`：与 vue-i18n locale 联动，zh-CN/en-US 动态切换
- `advanced-permission`：基于 JWT RBAC permissions 的 visibleOn 生成函数
- `advanced-perf`：lazyLoad/debug props、接口缓存、虚拟滚动最佳实践
- `advanced-editor`：amis-editor 三栏画布，顶部工具栏，v-model Schema 双向绑定

