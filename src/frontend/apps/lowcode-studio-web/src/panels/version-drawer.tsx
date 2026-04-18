import React, { useState } from 'react';
import { useQuery, useMutation } from '@tanstack/react-query';
import { SideSheet, List, Typography, Spin, Empty, Button, Modal, Toast, Space, Tag } from '@douyinfe/semi-ui';
import { lowcodeApi } from '../services/api-core';

/**
 * 版本管理抽屉（M14）。点击行项可：
 *  - diff 双版本（M14 S14-1）
 *  - rollback 到指定版本（M14 S14-1）
 */
export const VersionDrawer: React.FC<{ appId: string; visible: boolean; onClose: () => void }> = ({ appId, visible, onClose }) => {
  const [diffSelection, setDiffSelection] = useState<string[]>([]);
  const [showDiff, setShowDiff] = useState(false);

  const versionsQuery = useQuery({
    queryKey: ['lowcode-versions', appId],
    queryFn: () => lowcodeApi.apps.listVersions(appId),
    enabled: visible
  });

  const diffQuery = useQuery({
    queryKey: ['lowcode-version-diff', appId, ...diffSelection],
    queryFn: () => lowcodeApi.versions.diff(appId, diffSelection[0], diffSelection[1]),
    enabled: showDiff && diffSelection.length === 2
  });

  const rollbackMut = useMutation({
    mutationFn: (versionId: string) => lowcodeApi.versions.rollback(appId, versionId, '从版本管理抽屉触发'),
    onSuccess: () => Toast.success('回滚成功'),
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
                    {diffSelection.includes(v.id) ? '取消选择' : '选择对比'}
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
            <List
              dataSource={diffQuery.data.ops}
              renderItem={(op) => (
                <List.Item>
                  <Tag color={op.op === 'add' ? 'green' : op.op === 'remove' ? 'red' : 'amber'}>{op.op}</Tag>
                  <Typography.Text style={{ marginLeft: 8 }}>{op.path}</Typography.Text>
                  {op.before && <pre style={{ background: '#fff7f7', margin: 4, padding: 4, fontSize: 11 }}>- {op.before.slice(0, 200)}</pre>}
                  {op.after && <pre style={{ background: '#f7fff7', margin: 4, padding: 4, fontSize: 11 }}>+ {op.after.slice(0, 200)}</pre>}
                </List.Item>
              )}
            />
          </div>
        ) : <Empty title="选两个版本以查看 diff" />}
      </Modal>
    </SideSheet>
  );
};
