import React, { useEffect, useState } from 'react';
import { SideSheet, Typography, Banner, Tag, Space, Button, List } from '@douyinfe/semi-ui';
import * as signalR from '@microsoft/signalr';
import * as Y from 'yjs';
import { YjsSignalRProvider, CollabAwareness, CollabLockManager, type AwarenessUserState } from '@atlas/lowcode-collab-yjs';
import { t } from '../i18n';
import { useLowcodeStudioHost } from '../host';

/**
 * 协同编辑抽屉（M16 C16-1）。
 *
 * - 连接 /hubs/lowcode-collab，按 appId 加入 group
 * - YjsSignalRProvider：把 Yjs update 与 SignalR Hub 互通（不引入 Node 边车）
 * - CollabAwareness：在线用户与光标
 * - CollabLockManager：组件级编辑锁
 *
 * Studio 内 schema 仍以 REST draft 为权威；本抽屉首先解决"协同存在感"
 * （在线用户 / 锁状态展示），完整 schema 协同编辑由调用方在 lowcode-editor-canvas 内桥接 Yjs Map。
 */
export const CollabDrawer: React.FC<{ appId: string; userId: string; visible: boolean; onClose: () => void }> = ({ appId, userId, visible, onClose }) => {
  const { auth } = useLowcodeStudioHost();
  const [status, setStatus] = useState<'idle' | 'connecting' | 'connected' | 'disconnected' | 'error'>('idle');
  const [error, setError] = useState<string | null>(null);
  const [peers, setPeers] = useState<Array<{ clientId: number; userId?: string }>>([]);
  const [providerRef, setProviderRef] = useState<{ provider: YjsSignalRProvider; doc: Y.Doc; awareness: CollabAwareness; lock: CollabLockManager } | null>(null);

  useEffect(() => {
    if (!visible || providerRef) return;
    const conn = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/lowcode-collab', { accessTokenFactory: auth.accessTokenFactory })
      .withAutomaticReconnect()
      .build();

    const doc = new Y.Doc();
    const provider = new YjsSignalRProvider(doc, conn, { appId, userId });
    const awareness = new CollabAwareness(doc);
    const lock = new CollabLockManager(doc);

    setStatus('connecting');
    setError(null);
    provider.connect()
      .then(() => {
        setStatus('connected');
        awareness.setLocal({ userId, username: userId, color: pickColor(userId) });
        setProviderRef({ provider, doc, awareness, lock });
      })
      .catch((e: Error) => {
        setStatus('error');
        setError(e.message);
      });

    return () => {
      void provider.disconnect();
      doc.destroy();
      setProviderRef(null);
      setStatus('disconnected');
    };
  }, [appId, auth, userId, visible, providerRef]);

  // 简单轮询：从 awareness 读取 remote peers 列表
  useEffect(() => {
    if (!providerRef) return;
    const tick = () => {
      const remote = providerRef.awareness.getRemoteStates();
      const list: Array<{ clientId: number; userId?: string }> = [];
      remote.forEach((state: AwarenessUserState, clientId: number) => {
        list.push({ clientId, userId: state.userId });
      });
      setPeers(list);
    };
    tick();
    const t = setInterval(tick, 1000);
    return () => clearInterval(t);
  }, [providerRef]);

  return (
    <SideSheet title="协同编辑" visible={visible} onCancel={onClose} placement="right" size="medium">
      <Space style={{ marginBottom: 12 }}>
        <Tag color={status === 'connected' ? 'green' : status === 'connecting' ? 'blue' : status === 'error' ? 'red' : 'grey'}>
          {status}
        </Tag>
        <Typography.Text type="tertiary" style={{ fontSize: 12 }}>app #{appId}</Typography.Text>
      </Space>

      {status === 'connecting' && <Typography.Paragraph type="tertiary">正在连接 /hubs/lowcode-collab ...</Typography.Paragraph>}
      {error && <Banner type="danger" description={error} closeIcon={null} />}

      <Typography.Title heading={6} style={{ marginTop: 12 }}>在线协作者（{peers.length}）</Typography.Title>
      <List
        size="small"
        dataSource={peers}
        emptyContent={<Typography.Text type="tertiary">暂无其它在线协作者</Typography.Text>}
        renderItem={(p) => (
          <List.Item>
            <Space>
              <Tag color="blue" size="small">{p.clientId}</Tag>
              <Typography.Text>{p.userId ?? t('lowcode_studio.common.anonymous')}</Typography.Text>
            </Space>
          </List.Item>
        )}
      />

      <Typography.Paragraph type="tertiary" style={{ marginTop: 16, fontSize: 11 }}>
        协同传输：SignalR (无 Node 边车) + 自定义 YjsSignalRProvider。Schema 编辑 / 锁定 / 离线快照能力由
        @atlas/lowcode-collab-yjs 提供；Studio 阶段 A 接通存在感与连接状态，CRDT 编辑融合在画布完整接入后启用。
      </Typography.Paragraph>

      <Space style={{ marginTop: 16 }}>
        <Button onClick={() => { void providerRef?.provider.disconnect(); setProviderRef(null); setStatus('disconnected'); }} disabled={!providerRef}>断开</Button>
        <Button onClick={onClose}>关闭</Button>
      </Space>
    </SideSheet>
  );
};

function pickColor(seed: string): string {
  let hash = 0;
  for (const ch of seed) hash = (hash * 31 + ch.charCodeAt(0)) >>> 0;
  const palette = ['#1677ff', '#52c41a', '#faad14', '#eb2f96', '#722ed1', '#13c2c2'];
  return palette[hash % palette.length]!;
}
