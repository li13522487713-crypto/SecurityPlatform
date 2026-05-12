# 微流界面工具栏合并计划

## Summary

将微流编辑器中的浮动工具栏与顶部 StudioHeader 合并，只保留一个工具栏。

## Current State Analysis

### 当前两个工具栏

1. **StudioHeader** (`mendix-studio-core/src/components/studio-header.tsx`)
   - 位置：页面最顶部深色导航栏
   - 按钮：保存、校验、预览、发布、导出、撤销、重做
   - 功能：直接操作 store 数据

2. **MicroflowEditor 内部工具栏** (`mendix-microflow/src/editor/index.tsx` line ~6092-6200)
   - 位置：编辑器顶部浮动条
   - 内容：微流名称、版本标签、validation 状态、保存/运行/发布/版本/引用按钮
   - 功能：调用 MicroflowEditor 内部方法

### 控制机制

- `MicroflowEditor` 组件有 `toolbarMode` 参数：`"internal"` | `"external"`
- 当 `toolbarMode === "external"` 时，内部工具栏不渲染（line 6092 条件判断）
- 当 `toolbarMode === "external"` 时，编辑器通过 `editorRef` 暴露 `MicroflowEditorHandle` 接口，供外部调用 save/validate/run 等动作
- 当前 `mendix-studio-core/src/index.tsx` line 494 传入的是 `toolbarMode="internal"`

## Proposed Changes

### 文件 1: `src/frontend/packages/mendix/mendix-studio-core/src/index.tsx`

**变更内容**：
- 将 `MicroflowResourceEditorHost` 的 `toolbarMode` 从 `"internal"` 改为 `"external"`
- 这样编辑器内部将不再渲染浮动工具栏，避免双层工具栏问题

**具体修改**（line 494）：
```tsx
// 修改前
toolbarMode="internal"

// 修改后
toolbarMode="external"
```

### 文件 2: `src/frontend/packages/mendix/mendix-studio-core/src/components/studio-header.tsx`

**变更内容**：
- 在微流编辑器模式下，将 StudioHeader 的按钮与编辑器动作绑定
- 通过 `editorRef` 调用微流编辑器的 save/validate/run 等方法
- 需要在微流 tab 激活时，将 editorRef 传递给 StudioHeader

**具体修改**：
- StudioHeader 接收 editorRef 和当前是否为微流模式的信息
- 保存按钮：当在微流模式下，调用 `editorRef.current?.save()`
- 校验按钮：当在微流模式下，调用 `editorRef.current?.validate()`
- 其他按钮（预览、发布等）保持当前功能或后续扩展

### 文件 3: `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/MicroflowResourceEditorHost.tsx`

**变更内容**：
- 确认 `editorRef` 已正确传递给内部的 `MendixMicroflowEditorEntry`
- 确保 ref 接口暴露了 save、validate、run 等方法

## Assumptions & Decisions

### 已锁定决策

- 只保留 StudioHeader 作为唯一工具栏
- 内部工具栏完全移除，不显示任何浮动条
- 所有操作通过外部工具栏 + editorRef 接口完成

### 关键实现决策

- 使用已有的 `toolbarMode="external"` 机制，不需要新建桥接层
- `MicroflowEditorHandle` 接口已定义，直接复用
- StudioHeader 需要根据当前激活的 tab 类型动态绑定不同操作

## Verification Steps

### 代码验证

- 确认 `toolbarMode` 已改为 `"external"`
- 确认内部工具栏条件判断生效
- 确认外部按钮与编辑器 ref 绑定正确

### 必做命令

- 前端构建：`pnpm --dir src/frontend run build:app-web`
- E2E 测试：`pnpm --dir src/frontend exec playwright test -c playwright.app.config.ts e2e/app/mendix-studio-microflow-layout.spec.ts`

### 人机验收

- 在微流编辑器中只看到一个顶部工具栏
- 保存、校验、发布等按钮功能正常
- 没有双层工具栏的视觉问题
