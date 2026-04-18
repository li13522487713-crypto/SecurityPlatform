import React, { useEffect, useState } from 'react';
import { Modal, List, Tag, Typography, Space } from '@douyinfe/semi-ui';
import { listShortcuts, type ShortcutDescriptor } from '@atlas/lowcode-editor-canvas';
import { t } from '../i18n';

/**
 * 快捷键面板（M07 + 二轮深审 #16）。
 *
 * - 全局监听 Mod+/（macOS Cmd+/，Windows/Linux Ctrl+/）打开/关闭面板
 * - 列表按 SHORTCUT_REGISTRY 分组（edit / align / group / move / view / global）
 * - 平台敏感：自动把 Mod+ 替换为 ⌘+ / Ctrl+
 */
export const ShortcutPanel: React.FC = () => {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    const isMac = typeof navigator !== 'undefined' && /Mac/i.test(navigator.platform);
    const handler = (e: KeyboardEvent) => {
      const mod = isMac ? e.metaKey : e.ctrlKey;
      if (mod && e.key === '/') {
        e.preventDefault();
        setOpen((v) => !v);
      } else if (e.key === 'Escape' && open) {
        setOpen(false);
      }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open]);

  const all = listShortcuts();
  const byCategory = new Map<string, ShortcutDescriptor[]>();
  for (const s of all) {
    if (!byCategory.has(s.category)) byCategory.set(s.category, []);
    byCategory.get(s.category)!.push(s);
  }
  const isMac = typeof navigator !== 'undefined' && /Mac/i.test(navigator.platform);
  const formatKey = (k: string) => isMac ? k.replace(/Mod/g, '⌘').replace(/Alt/g, '⌥').replace(/Shift/g, '⇧') : k.replace(/Mod/g, 'Ctrl');

  return (
    <Modal
      title={t('lowcode_studio.shortcut.panel')}
      visible={open}
      onCancel={() => setOpen(false)}
      footer={null}
      width={680}
    >
      <Typography.Paragraph type="tertiary" style={{ marginBottom: 12 }}>
        共 {all.length} 项；按 {isMac ? '⌘+/' : 'Ctrl+/'} 关闭面板，Esc 取消选择
      </Typography.Paragraph>
      {Array.from(byCategory.entries()).map(([cat, items]) => (
        <div key={cat} style={{ marginBottom: 16 }}>
          <Typography.Title heading={6} style={{ margin: '0 0 6px' }}>
            <Tag color="blue">{cat}</Tag> <Typography.Text type="tertiary" style={{ fontSize: 12 }}>{items.length} 项</Typography.Text>
          </Typography.Title>
          <List
            size="small"
            split={false}
            dataSource={items}
            renderItem={(s) => (
              <List.Item style={{ padding: '4px 8px' }}>
                <Space style={{ width: '100%', justifyContent: 'space-between' }}>
                  <Typography.Text>{s.description}</Typography.Text>
                  <Tag color="grey" style={{ fontFamily: 'monospace' }}>{formatKey(s.defaultKey)}</Tag>
                </Space>
              </List.Item>
            )}
          />
        </div>
      ))}
    </Modal>
  );
};
