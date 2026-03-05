---
name: 前端UI改造PRD
overview: 将 Atlas Security Platform 前端全面改造为用友/金蝶风格的企业级安全管理平台 UI，涵盖设计系统重建、布局改造、核心业务页面交互规范化，共 55 个页面的统一视觉与交互升级。
todos:
  - id: design-system
    content: 阶段一：更新 CSS 变量（index.css）+ ConfigProvider 主题 token 注入（App.vue/main.ts）
    status: pending
  - id: layout-sidebar
    content: 阶段一：改造 MainLayout.vue 侧边栏（颜色#001529、高度48px）+ SidebarMenu 激活样式
    status: pending
  - id: homepage
    content: 阶段二：重构 HomePage.vue 为 Dashboard 布局（统计卡片+告警趋势图+待办列表）
    status: pending
  - id: alert-page
    content: 阶段二：改造 AlertPage.vue（安全语义色行着色、告警统计条、详情抽屉）
    status: pending
  - id: audit-page
    content: 阶段二：改造 AuditPage.vue（等保合规只读样式、操作类型着色、时间快选）
    status: pending
  - id: assets-page
    content: 阶段二：改造 AssetsPage.vue（资产概况卡片、状态指示点、风险等级Tag）
    status: pending
  - id: system-pages
    content: 阶段三：统一 system/* 下所有列表页（筛选区+操作栏+表格+分页），部门/菜单改左树右表
    status: pending
  - id: login-page
    content: 阶段四：改造 LoginPage.vue 为左右分屏安全平台风格
    status: pending
  - id: remaining-pages
    content: 阶段四：审批模块、监控、低代码等其余页面补齐统一规范
    status: pending
isProject: false
---

# Atlas Security Platform 前端 UI 改造 PRD

## 背景与目标

当前系统采用 Ant Design Vue 默认主题 + 简单 CSS 变量，风格偏向通用管理后台，缺乏安全平台的专业感与品牌一致性。参考用友 YonUI / iuap design、金蝶云星空等国产企业级软件的设计规范，结合等保安全平台的专项需求，对全部 55 个页面实施统一 UI 改造。

---

## 一、设计系统（Design System）重建

### 1.1 色彩体系

**品牌主色（保持 Ant Design v5 蓝色体系）**

- 主色：`#1677FF`（不变，与后端契约保持稳定）
- Hover：`#4096FF` / Active：`#0958D9`

**安全语义色（新增）**

```
严重（Critical）：#FF4D4F   —— 红色
高危（High）    ：#FA8C16   —— 橙色
中危（Medium）  ：#FADB14   —— 黄色
低危（Low）     ：#52C41A   —— 绿色
信息（Info）    ：#1677FF   —— 蓝色
```

**背景色调整（用友/金蝶风格，更专业中性）**

```
全局底色        ：#F0F2F5   （从 #F6F7FB 改为更中性的灰白）
容器背景        ：#FFFFFF
侧边栏背景      ：#001529   （Ant Design Pro 深海蓝，替代 #304156）
侧边栏激活      ：#1677FF
```

### 1.2 字体与排版


| 层级      | 用途          | 字号   | 字重  |
| ------- | ----------- | ---- | --- |
| Display | 大屏 KPI 数字   | 32px | 600 |
| H1      | 页面标题        | 20px | 600 |
| H2      | 卡片标题 / 分组标题 | 16px | 600 |
| Body    | 正文内容        | 14px | 400 |
| Caption | 辅助说明 / 表格辅助 | 12px | 400 |


字体栈：`"PingFang SC", "HarmonyOS Sans", "Microsoft YaHei", sans-serif`

### 1.3 间距与圆角（8px 栅格体系）

```
xs=4px  sm=8px  md=16px  lg=24px  xl=32px
圆角：sm=4px  md=6px  lg=8px  xl=12px
```

### 1.4 阴影层级

```
卡片阴影  ：0 1px 3px rgba(0,21,41,0.08)
下拉阴影  ：0 6px 16px rgba(0,0,0,0.12)
模态阴影  ：0 8px 24px rgba(0,0,0,0.15)
```

### 1.5 Ant Design Vue ConfigProvider 主题配置

在 `[src/frontend/Atlas.WebApp/src/main.ts](src/frontend/Atlas.WebApp/src/main.ts)` 或 `App.vue` 中通过 `ConfigProvider` 全局注入 token：

```typescript
theme: {
  token: {
    colorPrimary: '#1677FF',
    borderRadius: 6,
    fontSize: 14,
    colorBgLayout: '#F0F2F5',
    colorBgContainer: '#FFFFFF',
    fontFamily: '"PingFang SC", "HarmonyOS Sans", "Microsoft YaHei", sans-serif',
  }
}
```

---

## 二、布局改造（MainLayout.vue）

**当前问题：** 侧边栏背景色 `#304156`，顶部高度 50px，与用友/Ant Design Pro 规范不符。

**改造目标：**

```
┌─────────────────────────────────────────────────────────┐
│  侧边栏 208px（#001529 深海蓝）                          │
│  ┌──────────────────────────────────────────────────┐    │
│  │  Logo 区 64px（logo图片 + 系统名称）              │    │
│  ├──────────────────────────────────────────────────┤    │
│  │  菜单区（一级图标+文字，激活=蓝色左边框+蓝底）    │    │
│  └──────────────────────────────────────────────────┘    │
│                                                          │
│  主内容区                                                │
│  ┌──────────────────────────────────────────────────┐    │
│  │  顶部 Header 48px（白底）                        │    │
│  │  左：折叠按钮 | 面包屑                           │    │
│  │  右：全局搜索 | 告警铃铛（红色角标）| 主题 | 用户│    │
│  ├──────────────────────────────────────────────────┤    │
│  │  标签导航栏 TagsView（32px，淡灰底）              │    │
│  ├──────────────────────────────────────────────────┤    │
│  │  内容区（#F0F2F5 底色，padding 16px）            │    │
│  └──────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────┘
```

**关键变更：**

- 侧边栏宽度：`210px → 208px`，背景色：`#304156 → #001529`
- 顶部高度：`50px → 48px`
- 顶部增加**全局搜索框**（收起状态显示搜索图标，点击展开）
- 菜单激活状态：移除 `#409EFF`，改为白色文字 + `#1677FF` 背景条
- 涉及文件：`[src/frontend/Atlas.WebApp/src/layouts/MainLayout.vue](src/frontend/Atlas.WebApp/src/layouts/MainLayout.vue)`、`[src/frontend/Atlas.WebApp/src/styles/index.css](src/frontend/Atlas.WebApp/src/styles/index.css)`

---

## 三、核心页面改造规范

### 3.1 标准列表页（通用模式）

适用：资产、告警、审计、用户、角色、权限、菜单、部门、职位、项目、应用、字典等共 **20+ 个列表页**。

**页面结构（自上而下）：**

```
[页面 Header]  标题（H1）+ 副标题说明  +  右侧主操作按钮（新建/导入）
[筛选区]       搜索框 + 关键筛选项（≤4个常用）+ 展开高级筛选 + 重置/搜索
[操作栏]       批量删除 | 导出 | 刷新  右侧：列设置图标 | 密度切换
[数据表格]     复选框 | 数据列 | 操作列（查看/编辑/删除，超3项折叠入"更多"）
[分页]         左：共 N 条  右：每页条数选择 | 上/下页
```

**表格规范：**

- 行高：`48px`（中等密度，参考 Ant Design Pro）
- 操作列宽：固定右侧，≤3个操作时直接显示文字链，第4个起折叠
- 删除等危险操作：红色 `#FF4D4F`，点击弹确认 Modal
- 空状态：使用 `a-empty` + 引导文案

**创建/编辑：**

- ≤8 字段：使用 `a-modal`（宽度 520px）
- 9-20 字段：使用 `a-drawer`（宽度 640px）
- 21+ 字段：跳转独立页面（整页表单）

### 3.2 首页工作台（HomePage.vue）

**改造为用友风格 Dashboard：**

```
行1：统计卡片×4（资产总数 | 今日告警 | 待审批任务 | 合规指数）
     每卡片：图标 + 大数字(32px) + 描述 + 趋势箭头
行2：告警趋势图（7日折线，左2/3）| 告警类型分布（饼图，右1/3）
行3：待办列表（左1/2，最近5条待审批）| 最近审计（右1/2，最近5条）
行4：快捷入口（图标卡片，6-8个常用功能）
```

### 3.3 告警页（AlertPage.vue）

**安全平台专项规范：**

- 列表顶部增加**告警统计条**（各级别数量的彩色数字横条）
- 表格行按风险等级着色：Critical=浅红底、High=浅橙底（行背景色）
- 每行左侧加 `4px` 颜色竖条（左边框语义色）
- 严重告警行可配置"呼吸灯"闪烁 CSS 动效
- 状态 Tag：待处理（红）/处理中（橙）/已关闭（灰）/误报（蓝）
- 详情面板：右侧抽屉展开，包含告警原文（等宽代码块）+ 处置时间线

### 3.4 审计页（AuditPage.vue）

**等保合规要求：**

- 严格只读（无新增/删除按钮，仅导出）
- 顶部增加**统计区**：今日操作数 | 异常操作数 | 登录次数
- 操作类型 Tag 着色：查询=蓝，创建=绿，修改=橙，删除=红，登录=灰
- 结果 Tag：成功=绿，失败=红
- 支持快捷时间选择（今天/本周/本月/自定义）
- 异常操作（result=失败 或 非工时操作）行用浅红背景高亮

### 3.5 资产页（AssetsPage.vue）

- 列表顶部增加**资产概况卡片**（在线/离线/风险资产数量）
- 状态列：在线（●绿）/ 离线（●灰）/ 异常（●红，可加闪烁动效）
- 风险等级列：彩色 Tag（颜色对应安全语义色体系）
- 支持"拓扑视图"切换（图标按钮，当前版本可展示占位图，后续接入 X6）

### 3.6 系统管理页（system/*）

- 组织架构类（部门/菜单）：左树右表布局，树宽 `240px`
- 权限/角色：使用 `a-transfer` 穿梭框 或 树形 Checkbox 面板
- 用户页：支持从部门树过滤用户列表（联动）

### 3.7 登录页（LoginPage.vue）

**参考等保合规安全平台风格：**

- 左半屏：深色背景（`#001529`）+ 系统名称/版本 + 安全说明文案 + 品牌图形
- 右半屏：白色卡片，居中登录表单
- 品牌色按钮（主色蓝）
- 错误提示：表单项内联红色文字，不使用全局 toast

---

## 四、通用组件规范


| 组件     | 规范                               |
| ------ | -------------------------------- |
| 状态 Tag | 不超过 6 种颜色，语义统一（see §1.1）         |
| 操作按钮   | 主操作=实心蓝，次操作=边框蓝，危险=边框红           |
| 空状态    | 统一图标 + "暂无数据" + 引导文案             |
| 加载态    | Skeleton 骨架屏（表格/卡片），不用全屏 Loading |
| 确认弹窗   | 标题前加 `！` 警告图标，危险操作按钮红色           |
| 分页     | 默认 20 条/页，显示总条数，支持跳页             |
| 搜索下拉   | 默认展示 20 条，必须提供搜索框支持远程检索          |


---

## 五、实施计划（分阶段）

### 阶段一：设计系统与布局基础（P0，约 3 天）

- `styles/index.css`：更新 CSS 变量（颜色/间距/字体）
- `main.ts` / `App.vue`：接入 `ConfigProvider` 全局 token 覆盖
- `MainLayout.vue`：侧边栏配色、Header 高度、新增全局搜索图标
- `SidebarMenu.vue`：激活样式改造（白色文字+蓝色背景条）

### 阶段二：核心业务页面（P0，约 5 天）

- `HomePage.vue`：Dashboard 重构（统计卡片+图表布局）
- `AlertPage.vue`：安全语义色着色、行颜色标记、详情抽屉
- `AuditPage.vue`：只读合规样式、操作类型着色、时间快选
- `AssetsPage.vue`：资产概况卡片、状态指示点

### 阶段三：系统管理页面（P1，约 3 天）

- `system/` 下全部页面统一列表页模式（筛选区+操作栏+表格+分页）
- 部门/菜单页改为左树右表布局

### 阶段四：登录页与其余页面（P1，约 2 天）

- `LoginPage.vue`：左右分屏安全平台风格
- 审批模块、监控、低代码等页面补齐统一规范

---

## 六、不改动范围

- `ApprovalDesignerPage.vue` / `WorkflowDesignerPage.vue`：X6 画布设计器，有独立样式，仅做外框调整
- `AppBuilderPage.vue` / `FormDesignerPage.vue`：AMIS/vform3 引擎，不改内部渲染
- `VisualizationDesignerPage.vue`：独立可视化设计器
- 所有后端接口、类型定义、API 服务文件不受影响

