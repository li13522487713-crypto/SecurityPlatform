# PRD: Phase 1 - 多设备预览与响应式设计 (Multi-device Preview)

## 1. 业务目标
为低代码引擎 (AMIS FormDesigner) 和未来的可视化页面提供多终端 (Desktop, Tablet, Mobile) 的响应式设计预览环境。通过 iframe 沙箱模拟不同设备的分辨率、屏幕方向，辅助平台使用者搭建多设备自适应页面。

## 2. 功能需求

### 2.1 设备切换控件 (`DeviceToolbar.vue`)
- 提供常见的设备预设切换功能 (Desktop `100%`, Tablet `768x1024`, Mobile `375x667`)。
- 提供屏幕方向 (Landscape/Portrait) 翻转功能。
- 缩放控制 (Scale Control)：当设备尺寸超出可用容器时，支持整体缩放 (`zoom`) 预览。

### 2.2 多设备渲染容器 (`DeviceFrame.vue` iframe 沙箱)
- 包装为完整的预览组件，接收 URL、HTML内容 或是直接接受 Vue/React 节点的渲染注入。
- 使用 `iframe` 完全隔离主应用 CSS/Javascript 以防止污染。
- 将低代码画布（本需求中涉及 `AmisEditor` 的 preview 模式）渲染入 iframe 内。

### 2.3 `FormDesignerPage.vue` 引擎接入
- 屏蔽 AMIS 原版的简易移动端预览，接入 `DeviceToolbar` 和 `DeviceFrame`。
- 与原有的 Schema Editor 和右侧配置面板解耦，仅在预览区域展示沙箱容器。

## 3. 架构设计与实现计划

### 3.1 Frontend - Component Tier (前端组件层)
- **`components/device-preview/DeviceToolbar.vue`**: UI 操作栏，通过 `v-model` 向外输出包含宽度、高度、缩放比例的 `DeviceState` 对象。
- **`components/device-preview/DeviceFrame.vue`**: 
  - 通过注入/计算出的宽高渲染外层包装框 (类似壳子手机形状的 mock)。
  - 内嵌 `<iframe />`。如果由于 AMIS 无法干净地跨文档渲染，降级为使用 `div` 与严格的宽/高限定 + `overflow: auto`。为了本平台集成，由于 AMIS React 需要注入父级应用作用域才能双向绑定，我们暂且采用 **带有响应式 `wrapper` 的 `div`** 作为第一版 “沙箱”；未来如果彻底隔离可启用真实 iframe。

### 3.2 Frontend - Integration Tier (前端集成层)
- **`FormDesignerPage.vue` 改造**:
  - 原左侧画布区，将 AMIS Editor 包入 `DeviceWrapper` 控制宽度。
  - 通过点击工具栏中相应的设备，更新画布宽高并动态注入媒体查询相应的标识（AMIS 的 `isMobile` props）。

## 4. 重点约束
- **体验平滑**: 设备切换无需造成表单配置或草稿状态的丢失。
- **等保2.0合规映射**: 本块为前端视口能力增强，本身不涉及敏感落库与跨边界。因此操作日志暂不涉及，但需确保存储到 `FormDefinition` 的 Schema 在 Desktop 与 Mobile 下保存格式统一。
