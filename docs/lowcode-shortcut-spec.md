# 低代码快捷键完整清单（lowcode-shortcut-spec）

> 状态：M04 落地。
> 范围：M04 lowcode-editor-canvas + M07 lowcode-studio-web 共 ≥ 40 项。

## 1. 编辑类（≥ 9）

| ID | 默认键位 | 描述 |
| --- | --- | --- |
| edit.undo | Mod+Z | 撤销 |
| edit.redo | Mod+Shift+Z | 重做 |
| edit.copy | Mod+C | 复制 |
| edit.cut | Mod+X | 剪切 |
| edit.paste | Mod+V | 粘贴 |
| edit.pasteInPlace | Mod+Shift+V | 同位粘贴 |
| edit.delete | Delete | 删除选中 |
| edit.selectAll | Mod+A | 全选 |
| edit.deselect | Escape | 取消选择 |

## 2. 对齐类（≥ 6）

| ID | 默认键位 | 描述 |
| --- | --- | --- |
| align.left | Mod+Alt+L | 左对齐 |
| align.centerH | Mod+Alt+H | 水平居中 |
| align.right | Mod+Alt+R | 右对齐 |
| align.top | Mod+Alt+T | 顶端对齐 |
| align.centerV | Mod+Alt+V | 垂直居中 |
| align.bottom | Mod+Alt+B | 底端对齐 |

## 3. 分组类（≥ 5）

| ID | 默认键位 | 描述 |
| --- | --- | --- |
| group.group | Mod+G | 分组 |
| group.ungroup | Mod+Shift+G | 解组 |
| group.toggleLock | Mod+L | 锁定/解锁 |
| group.toggleVisible | Mod+Shift+H | 显隐 |
| group.center | Mod+Shift+C | 居中到画布 |

## 4. 移动类（≥ 14）

| ID | 默认键位 | 描述 |
| --- | --- | --- |
| move.left1 / move.right1 / move.up1 / move.down1 | ArrowLeft/Right/Up/Down | 移动 1px |
| move.left10 / move.right10 / move.up10 / move.down10 | Shift+ArrowXxx | 移动 10px |
| move.wrapInContainer | Mod+Alt+G | 包裹容器 |
| move.unwrapContainer | Mod+Alt+U | 解包容器 |
| move.bringToFront | Mod+] | 前置 |
| move.sendToBack | Mod+[ | 后置 |
| move.bringForward | Mod+Alt+] | 上移一层 |
| move.sendBackward | Mod+Alt+[ | 下移一层 |

## 5. 视图类（≥ 6）

| ID | 默认键位 | 描述 |
| --- | --- | --- |
| view.zoomIn | Mod+= | 放大 |
| view.zoomOut | Mod+- | 缩小 |
| view.zoomReset | Mod+0 | 实际大小 |
| view.fitScreen | Mod+1 | 适应屏幕 |
| view.toggleGrid | Mod+\` | 网格开关 |
| view.toggleGuides | Mod+; | 参考线开关 |

## 6. 全局类（≥ 5）

| ID | 默认键位 | 描述 |
| --- | --- | --- |
| global.save | Mod+S | 保存（触发 autosave 同步落 `/api/v1/lowcode/apps/{id}/draft`） |
| global.commandPalette | Mod+Shift+P | 命令面板 |
| global.help | Mod+/ | 快捷键面板 |
| global.tabSwitch | Tab | 切换 Tab |
| global.focusOutline | Mod+1 | 焦点结构树 |

## 7. 冲突检测策略

- `detectKeymapConflicts` 函数检测同一 key 是否被多个 id 占用；
- 默认清单允许 `Mod+1` 同时被 `view.fitScreen` 与 `global.focusOutline` 占用（语义上下文相关），
  上层 UI 在调用时按当前焦点（画布 vs 结构树）路由。
- 用户自定义键位通过 `KeymapOverrides.overrides` 覆盖；UI 加载后应再次 `detectKeymapConflicts` 给出警告。

## 8. 用户自定义键位策略

- 持久化：`localStorage` 键 `atlas_lowcode_keymap_overrides`（与 `atlas_locale` 同源约定）。
- 重置：在快捷键面板提供"恢复默认"按钮 → 清空 overrides。
- CI 守门：`SHORTCUT_COUNT` 常量必须保持 ≥ 40，否则 spec 测试失败。
