import { useEffect } from 'react';
import { Toast } from '@douyinfe/semi-ui';
import type { AppSchema, ComponentSchema } from '@atlas/lowcode-schema';
import { lowcodeApi } from '../services/api-core';
import { useStudioSelection } from '../stores/selection-store';

interface CommandsOptions {
  appId: string;
}

/**
 * Studio 全局快捷键命令绑定（M07 + M04 keymap）：
 *  - Delete / Backspace：删除选中组件（与 Inspector 删除按钮等价）
 *  - Escape：取消选中
 *  - Mod+S：保存（不展开 Modal，直接 snapshot 一份默认 label）
 *
 * 复杂多键（撤销 / 重做 / 复制粘贴）由 lowcode-editor-canvas/history 在画布完整接入阶段绑定；
 * 此处仅接入与 schema CRUD 强相关的"删除 / 取消 / 保存"3 项，避免多端按键冲突。
 *
 * 强约束：所有 schema mutation 走 GET draft → mutate → POST autosave，触发 ILowCodePreviewSignal 推送。
 */
export function useStudioCommands(opts: CommandsOptions): void {
  const { appId } = opts;
  const { selectedComponentId, setSelectedComponentId } = useStudioSelection();

  useEffect(() => {
    if (!appId) return;
    const isMac = typeof navigator !== 'undefined' && /Mac/i.test(navigator.platform);
    const handler = async (e: KeyboardEvent) => {
      const target = e.target as HTMLElement | null;
      if (target && (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable)) {
        return; // 在输入框内按键不抢占（避免删除文本误触）
      }

      if (e.key === 'Escape') {
        if (selectedComponentId) {
          setSelectedComponentId(null);
          e.preventDefault();
        }
        return;
      }

      if ((e.key === 'Delete' || e.key === 'Backspace') && selectedComponentId) {
        e.preventDefault();
        try {
          const draft = await lowcodeApi.apps.getDraft(appId);
          const app = JSON.parse(draft.schemaJson) as AppSchema;
          let removed = false;
          for (const page of app.pages ?? []) {
            if (page.root.id === selectedComponentId) {
              Toast.warning('禁止删除页面根节点');
              return;
            }
            if (deleteById(page.root, selectedComponentId)) {
              removed = true;
              break;
            }
          }
          if (!removed) {
            Toast.warning('未找到选中节点');
            return;
          }
          await lowcodeApi.apps.autosave(appId, JSON.stringify(app));
          Toast.success('已删除');
          setSelectedComponentId(null);
        } catch (err) {
          Toast.error((err as Error).message);
        }
        return;
      }

      const mod = isMac ? e.metaKey : e.ctrlKey;
      if (mod && e.key.toLowerCase() === 's') {
        e.preventDefault();
        try {
          const label = `quick-${new Date().toISOString().slice(0, 19).replace(/[-:T]/g, '')}`;
          const r = await lowcodeApi.apps.snapshot(appId, label, '快捷键 Mod+S 触发');
          Toast.success(`已保存版本（${r.versionId}）`);
        } catch (err) {
          Toast.error((err as Error).message);
        }
      }
    };

    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [appId, selectedComponentId, setSelectedComponentId]);
}

function deleteById(node: ComponentSchema, id: string): boolean {
  if (node.children) {
    const idx = node.children.findIndex((c) => c.id === id);
    if (idx >= 0) {
      node.children.splice(idx, 1);
      return true;
    }
    for (const c of node.children) {
      if (deleteById(c, id)) return true;
    }
  }
  if (node.slots) {
    for (const list of Object.values(node.slots)) {
      const idx = list.findIndex((c) => c.id === id);
      if (idx >= 0) {
        list.splice(idx, 1);
        return true;
      }
      for (const c of list) {
        if (deleteById(c, id)) return true;
      }
    }
  }
  return false;
}
