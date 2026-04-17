/**
 * 完整快捷键体系（M04 C04-7）。
 *
 * ≥ 40 项快捷键的注册中心 + 描述符；UI 通过 Ctrl+/ 调用 listShortcuts() 渲染面板。
 * 与 docs/lowcode-shortcut-spec.md 完全对齐。
 *
 * 设计要点：
 * - 不强依赖 react / DOM；本模块仅维护 ShortcutDescriptor + 用户自定义键位覆盖；
 *   实际监听由 M07 lowcode-studio-web 用 useEffect+keydown 接入。
 * - 键位描述用统一格式：'Mod+Z' / 'Mod+Shift+Z' / 'ArrowLeft' / 'Shift+ArrowLeft' / 'Delete' 等。
 *   Mod = macOS 上 Cmd，其他平台 Ctrl。
 */

export type ShortcutCategory = 'edit' | 'align' | 'group' | 'move' | 'view' | 'global';

export interface ShortcutDescriptor {
  id: string;
  category: ShortcutCategory;
  /** 默认键位（按平台中性写法）。*/
  defaultKey: string;
  /** 用户友好的描述（显示在快捷键面板）。*/
  description: string;
  /** i18n key（与 zh-CN/en-US 对齐）。*/
  i18nKey?: string;
}

export const SHORTCUT_REGISTRY: ReadonlyArray<ShortcutDescriptor> = [
  // 编辑类（≥ 9）
  { id: 'edit.undo', category: 'edit', defaultKey: 'Mod+Z', description: '撤销' },
  { id: 'edit.redo', category: 'edit', defaultKey: 'Mod+Shift+Z', description: '重做' },
  { id: 'edit.copy', category: 'edit', defaultKey: 'Mod+C', description: '复制' },
  { id: 'edit.cut', category: 'edit', defaultKey: 'Mod+X', description: '剪切' },
  { id: 'edit.paste', category: 'edit', defaultKey: 'Mod+V', description: '粘贴' },
  { id: 'edit.pasteInPlace', category: 'edit', defaultKey: 'Mod+Shift+V', description: '同位粘贴' },
  { id: 'edit.delete', category: 'edit', defaultKey: 'Delete', description: '删除选中' },
  { id: 'edit.selectAll', category: 'edit', defaultKey: 'Mod+A', description: '全选' },
  { id: 'edit.deselect', category: 'edit', defaultKey: 'Escape', description: '取消选择' },

  // 对齐类（≥ 6）
  { id: 'align.left', category: 'align', defaultKey: 'Mod+Alt+L', description: '左对齐' },
  { id: 'align.centerH', category: 'align', defaultKey: 'Mod+Alt+H', description: '水平居中' },
  { id: 'align.right', category: 'align', defaultKey: 'Mod+Alt+R', description: '右对齐' },
  { id: 'align.top', category: 'align', defaultKey: 'Mod+Alt+T', description: '顶端对齐' },
  { id: 'align.centerV', category: 'align', defaultKey: 'Mod+Alt+V', description: '垂直居中' },
  { id: 'align.bottom', category: 'align', defaultKey: 'Mod+Alt+B', description: '底端对齐' },

  // 分组类（≥ 5）
  { id: 'group.group', category: 'group', defaultKey: 'Mod+G', description: '分组' },
  { id: 'group.ungroup', category: 'group', defaultKey: 'Mod+Shift+G', description: '解组' },
  { id: 'group.toggleLock', category: 'group', defaultKey: 'Mod+L', description: '锁定/解锁' },
  { id: 'group.toggleVisible', category: 'group', defaultKey: 'Mod+Shift+H', description: '显隐' },
  { id: 'group.center', category: 'group', defaultKey: 'Mod+Shift+C', description: '居中到画布' },

  // 移动类（≥ 9）
  { id: 'move.left1', category: 'move', defaultKey: 'ArrowLeft', description: '左移 1px' },
  { id: 'move.right1', category: 'move', defaultKey: 'ArrowRight', description: '右移 1px' },
  { id: 'move.up1', category: 'move', defaultKey: 'ArrowUp', description: '上移 1px' },
  { id: 'move.down1', category: 'move', defaultKey: 'ArrowDown', description: '下移 1px' },
  { id: 'move.left10', category: 'move', defaultKey: 'Shift+ArrowLeft', description: '左移 10px' },
  { id: 'move.right10', category: 'move', defaultKey: 'Shift+ArrowRight', description: '右移 10px' },
  { id: 'move.up10', category: 'move', defaultKey: 'Shift+ArrowUp', description: '上移 10px' },
  { id: 'move.down10', category: 'move', defaultKey: 'Shift+ArrowDown', description: '下移 10px' },
  { id: 'move.wrapInContainer', category: 'move', defaultKey: 'Mod+Alt+G', description: '包裹容器' },
  { id: 'move.unwrapContainer', category: 'move', defaultKey: 'Mod+Alt+U', description: '解包容器' },
  { id: 'move.bringToFront', category: 'move', defaultKey: 'Mod+]', description: '前置' },
  { id: 'move.sendToBack', category: 'move', defaultKey: 'Mod+[', description: '后置' },
  { id: 'move.bringForward', category: 'move', defaultKey: 'Mod+Alt+]', description: '上移一层' },
  { id: 'move.sendBackward', category: 'move', defaultKey: 'Mod+Alt+[', description: '下移一层' },

  // 视图类（≥ 6）
  { id: 'view.zoomIn', category: 'view', defaultKey: 'Mod+=', description: '放大' },
  { id: 'view.zoomOut', category: 'view', defaultKey: 'Mod+-', description: '缩小' },
  { id: 'view.zoomReset', category: 'view', defaultKey: 'Mod+0', description: '实际大小' },
  { id: 'view.fitScreen', category: 'view', defaultKey: 'Mod+1', description: '适应屏幕' },
  { id: 'view.toggleGrid', category: 'view', defaultKey: 'Mod+`', description: '网格开关' },
  { id: 'view.toggleGuides', category: 'view', defaultKey: 'Mod+;', description: '参考线开关' },

  // 全局类（≥ 5）
  { id: 'global.save', category: 'global', defaultKey: 'Mod+S', description: '保存' },
  { id: 'global.commandPalette', category: 'global', defaultKey: 'Mod+Shift+P', description: '命令面板' },
  { id: 'global.help', category: 'global', defaultKey: 'Mod+/', description: '快捷键面板' },
  { id: 'global.tabSwitch', category: 'global', defaultKey: 'Tab', description: '切换 Tab' },
  { id: 'global.focusOutline', category: 'global', defaultKey: 'Mod+1', description: '焦点结构树' }
];

/** 列出全部快捷键（按 category 分组）。*/
export function listShortcuts(): ReadonlyArray<ShortcutDescriptor> {
  return SHORTCUT_REGISTRY;
}

export interface KeymapOverrides {
  /** 用户自定义覆盖：id → key。*/
  overrides: Record<string, string>;
}

/** 合并默认 + 用户覆盖，输出实际生效键位映射。*/
export function resolveKeymap(overrides?: KeymapOverrides): Record<string, string> {
  const out: Record<string, string> = {};
  for (const s of SHORTCUT_REGISTRY) {
    out[s.id] = overrides?.overrides[s.id] ?? s.defaultKey;
  }
  return out;
}

/** 检测自定义键位与默认键位之间是否有冲突。*/
export function detectKeymapConflicts(map: Record<string, string>): Array<{ key: string; ids: string[] }> {
  const inverse = new Map<string, string[]>();
  for (const [id, key] of Object.entries(map)) {
    const list = inverse.get(key) ?? [];
    list.push(id);
    inverse.set(key, list);
  }
  return Array.from(inverse.entries())
    .filter(([, ids]) => ids.length > 1)
    .map(([key, ids]) => ({ key, ids }));
}

/** ≥ 40 项强约束守门（CI 兜底）。*/
export const SHORTCUT_COUNT = SHORTCUT_REGISTRY.length;
