import React, { useState } from 'react';
import { useQuery, useMutation } from '@tanstack/react-query';
import { SideSheet, List, Typography, Spin, Empty, Button, Modal, Toast, Space, Tag } from '@douyinfe/semi-ui';
import { groupDiffsByGroup } from '@atlas/lowcode-versioning-client';
import { t } from '../i18n';
import { useLowcodeStudioHost } from '../host';

/**
 * 版本管理抽屉（M14）。点击行项可：
 *  - diff 双版本（M14 S14-1）
 *  - rollback 到指定版本（M14 S14-1）
 */
export const VersionDrawer: React.FC<{ appId: string; visible: boolean; onClose: () => void }> = ({ appId, visible, onClose }) => {
  const [diffSelection, setDiffSelection] = useState<string[]>([]);
  const [showDiff, setShowDiff] = useState(false);
  const { api } = useLowcodeStudioHost();

  const versionsQuery = useQuery({
    queryKey: ['lowcode-versions', appId],
    queryFn: () => api.apps.listVersions(appId),
    enabled: visible
  });

  const diffQuery = useQuery({
    queryKey: ['lowcode-version-diff', appId, ...diffSelection],
    queryFn: () => api.versions.diff(appId, diffSelection[0], diffSelection[1]),
    enabled: showDiff && diffSelection.length === 2
  });

  const rollbackMut = useMutation({
    mutationFn: (versionId: string) => api.versions.rollback(appId, versionId, t('lowcode_studio.common.fromVersionDrawer')),
    onSuccess: () => Toast.success(t('lowcode_studio.common.rollbackSuccess')),
    onError: (e: Error) => Toast.error(e.message)
  });

  const toggleSelect = (id: string) => {
    if (diffSelection.includes(id)) {
      setDiffSelection(diffSelection.filter((s) => s !== id));
    } else if (diffSelection.length < 2) {
      setDiffSelection([...diffSelection, id]);
    } else {
      setDiffSelection([diffSelection[1], id]);
    }
  };

  return (
    <SideSheet title="版本管理" visible={visible} onCancel={onClose} placement="right" size="large">
      <Space style={{ marginBottom: 16 }}>
        <Typography.Text>已选 {diffSelection.length}/2 个版本</Typography.Text>
        <Button disabled={diffSelection.length !== 2} onClick={() => setShowDiff(true)}>对比 diff</Button>
      </Space>

      {versionsQuery.isLoading ? <Spin /> : (
        <List
          dataSource={versionsQuery.data ?? []}
          emptyContent={<Empty title="尚无版本" />}
          renderItem={(v) => (
            <List.Item
              header={<Typography.Text strong>{v.versionLabel}</Typography.Text>}
              extra={
                <Space>
                  <Button size="small" type={diffSelection.includes(v.id) ? 'primary' : 'tertiary'} onClick={() => toggleSelect(v.id)}>
                    {diffSelection.includes(v.id) ? t('lowcode_studio.common.unselect') : t('lowcode_studio.common.selectForCompare')}
                  </Button>
                  <Button size="small" onClick={() => rollbackMut.mutate(v.id)} loading={rollbackMut.isPending}>回滚</Button>
                  {v.isSystemSnapshot && <Tag size="small" color="grey">系统</Tag>}
                </Space>
              }
            >
              <Typography.Paragraph type="tertiary" style={{ margin: 0, fontSize: 12 }}>
                {new Date(v.createdAt).toLocaleString()} · {v.note ?? ''}
              </Typography.Paragraph>
            </List.Item>
          )}
        />
      )}

      <Modal title="版本 diff" visible={showDiff} onCancel={() => setShowDiff(false)} footer={null} width={720}>
        {diffQuery.isLoading ? <Spin /> : diffQuery.data ? (
          <div>
            <Typography.Title heading={6}>{diffQuery.data.fromLabel} → {diffQuery.data.toLabel}</Typography.Title>
            <Typography.Paragraph type="tertiary" style={{ marginBottom: 8 }}>
              共 {diffQuery.data.ops.length} 项变更（按 path 顶段分组：基础 / 页面 / 变量 / 内容参数 / 其它）
            </Typography.Paragraph>
            {Object.entries(groupDiffsByGroup(diffQuery.data.ops)).map(([group, ops]) => (
              <div key={group} style={{ marginBottom: 12 }}>
                <Typography.Text strong style={{ fontSize: 13, color: '#444' }}>
                  {group} <Tag size="small">{ops.length}</Tag>
                </Typography.Text>
                <List
                  dataSource={ops}
                  renderItem={(op) => (
                    <List.Item>
                      <Tag color={op.op === 'add' ? 'green' : op.op === 'remove' ? 'red' : 'amber'}>{op.op}</Tag>
                      <Typography.Text style={{ marginLeft: 8, fontSize: 12 }}>{op.path}</Typography.Text>
                      {op.before && <pre style={{ background: '#fff7f7', margin: 4, padding: 4, fontSize: 11 }}>- {op.before.slice(0, 200)}</pre>}
                      {op.after && <pre style={{ background: '#f7fff7', margin: 4, padding: 4, fontSize: 11 }}>+ {op.after.slice(0, 200)}</pre>}
                    </List.Item>
                  )}
                />
              </div>
            ))}
          </div>
        ) : <Empty title="选两个版本以查看 diff" />}
      </Modal>
    </SideSheet>
  );
};
