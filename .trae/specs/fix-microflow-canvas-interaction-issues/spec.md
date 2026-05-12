# Mendix Studio 微流画布交互问题修复 Spec

## Why

用户反馈微流画布交互存在异常但无法具体描述。经全面自查发现 9 个关键问题，涵盖事件冲突、面板状态机不一致、Quick Insert 面板关闭逻辑等。修复目标：对齐 Mendix Studio Pro 标准交互模式，同时保留"画布直达"优化方向。

## Mendix Studio Pro 标准交互参考

| 交互场景 | Mendix Studio Pro 标准行为 | 本项目当前行为 | 修复方向 |
|----------|---------------------------|---------------|----------|
| 节点单击 | 选中节点，属性面板自动显示 | ✅ 已修复（自动打开） | 保持 |
| 节点双击 | 打开节点配置对话框 | 打开属性面板 | **改为打开节点属性对话框** |
| 空白单击 | 取消选择，关闭属性面板 | 取消选择 | ✅ 一致 |
| 空白双击 | 无操作 | 打开 Quick Insert | **保留优化，非 Mendix 标准** |
| 右键节点 | 上下文菜单（Properties/Copy/Delete） | ✅ 一致 | 保持 |
| 右键空白 | 上下文菜单（Paste/Insert） | ✅ 一致 | 保持 |
| 新增节点 | 从 App Explorer 拖拽 或 右键 Insert | 左侧面板拖拽 + Quick Insert | **保留 Quick Insert 优化** |
| 属性面板 | 选中时显示，空白点击关闭 | 选中显示，空白不关闭 | **改为空白点击关闭** |
| 左侧资源树 | 始终显示 App Explorer | 默认折叠 | **改为默认展开（Mendix 风格）** |
| 工具栏 | 顶部文档操作 + 画布浮动缩放 | 双工具栏 | **已优化** |
| 快捷键 Ctrl+K | 命令面板 | ✅ 一致 | 保持 |
| 拖拽节点后点击 | 点击其他节点正常选择 | 第一次点击被抑制 | **修复** |

## What Changes

- 修复拖拽后 `suppressNextNodeClickRef` 过度抑制导致有意点击被忽略
- 修复全局 click listener 同时关闭三个面板且频繁重注册的问题
- 修复 `isTempExpanded` 永不重置导致面板状态不一致
- 修复工具栏按钮点击未阻止传播到画布容器的 onClickCapture
- 修复 `onDoubleClick` 事件阶段不一致（冒泡 vs 捕获）
- 修复 Quick Insert 面板位置未处理负值边界
- 修复节点双击行为：对齐 Mendix 标准（打开属性对话框而非仅打开面板）
- 修复属性面板关闭策略：对齐 Mendix 标准（空白点击关闭）
- 修复左侧资源树默认状态：对齐 Mendix 标准（默认展开 App Explorer）
- 更新 E2E 测试覆盖所有修复场景

## Impact

- 受影响能力：画布点击/双击/右键、属性面板自动打开/关闭、Quick Insert、左侧资源树折叠
- 受影响代码：
  - `src/frontend/packages/mendix/mendix-microflow/src/flowgram/FlowGramMicroflowNativeCanvas.tsx`
  - `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx`
  - `src/frontend/packages/mendix/mendix-studio-core/src/components/explorer-split-layout.tsx`
  - `src/frontend/e2e/app/mendix-studio-microflow-layout.spec.ts`

## ADDED Requirements

### Requirement: 拖拽后点击不应被误抑制
系统 SHALL 在拖拽结束后仅抑制拖拽终点处的误触 click，而不抑制后续有意的节点点击。

#### Scenario: 拖拽节点后点击另一节点
- **WHEN** 用户拖拽节点 A 后点击节点 B
- **THEN** 节点 B 的点击事件正常触发，属性面板打开

### Requirement: 面板关闭逻辑独立且精准
系统 SHALL 对不同面板使用独立的关闭逻辑，点击某面板内部不应关闭其他无关面板。

#### Scenario: Quick Insert 面板打开时点击 Context Menu
- **WHEN** Quick Insert 面板已打开，用户右键画布节点
- **THEN** Context Menu 打开，Quick Insert 保持或关闭（取决于点击位置），而不是同时关闭

### Requirement: Quick Insert 面板边界处理
系统 SHALL 确保 Quick Insert 面板始终完全可见，不会溢出视口任何边界。

#### Scenario: 在画布左上角触发 Quick Insert
- **WHEN** 用户双击画布左上角区域
- **THEN** Quick Insert 面板显示在触发点右下方，完全可见

## MODIFIED Requirements

### Requirement: 属性面板自动打开与关闭策略
**原行为**: 任何选择变更都强制 `setLeftOpen(false); setRightOpen(true)`，空白点击不关闭
**Mendix 标准行为**: 节点单击选中时自动打开属性面板，点击空白画布取消选择时关闭属性面板
**新行为**: 
- 节点单击 → 打开属性面板
- 点击空白 → 取消选择 + 关闭属性面板
- 调试/Trace/Problem 点击 → 仅更新选择，不强制关闭左侧面板

### Requirement: 节点双击行为
**原行为**: 双击节点打开属性面板
**Mendix 标准行为**: 双击节点打开节点配置对话框（Properties Dialog）
**新行为**: 双击节点打开属性对话框（Modal 形式的完整属性编辑），而非仅切换右侧面板

### Requirement: ExplorerSplitLayout 临时展开状态
**原行为**: `isTempExpanded` 一旦设置永不重置
**Mendix 标准行为**: App Explorer 始终展开显示
**新行为**: 
- 微流设计器模式下 App Explorer 默认展开（对齐 Mendix 标准）
- `isTempExpanded` 在失焦/点击画布时自动重置
- 保留手动折叠按钮供高级用户按需使用

## REMOVED Requirements

无删除功能，仅修复交互逻辑。
