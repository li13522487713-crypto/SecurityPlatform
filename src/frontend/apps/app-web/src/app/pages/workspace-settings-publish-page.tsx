import { useCallback, useEffect, useMemo, useState } from "react";
import { Banner, Button, Card, Empty, Spin, Table, TabPane, Tabs, Tag, Toast } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { useNavigate, useParams } from "react-router-dom";
import {
  AddChannelModal,
  ChannelDetailRouter,
  ChannelsListPanel,
  type PublishChannelListItem
} from "@atlas/module-studio-react";
import {
  agentEditorPath,
  workflowEditorPath,
  workspaceProjectsPath,
  workspaceSettingsPublishPath,
  type WorkspaceSettingsPublishTab
} from "@atlas/app-shell-shared";
import { useAppI18n } from "../i18n";
import { useWorkspaceContext } from "../workspace-context";
import { getAiAssistantsPaged } from "../../services/api-ai-assistant";
import type { AgentListItem } from "../../services/api-agent";
import { listWorkflows, type WorkflowListItem } from "../../services/api-workflow";
import {
  createWorkspacePublishChannel,
  deleteWorkspacePublishChannel,
  getWorkspaceChannelActiveRelease,
  listWorkspacePublishChannelsPage,
  listPublishChannelCatalog,
  publishChannelsHttpJson,
  reauthorizeWorkspacePublishChannel
} from "../../services/api-publish-channels";
import { createLowcodeProjectAppGateway, type ProjectAppCard } from "../gateways/project-app-gateway";
import { WorkspaceSettingsLayout } from "../layouts/workspace-settings-layout";

type Tab = WorkspaceSettingsPublishTab;

const TAB_KEYS: Tab[] = ["agents", "apps", "workflows", "channels"];

export function WorkspaceSettingsPublishPage() {
  const { t, locale } = useAppI18n();
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const params = useParams<{ tab?: string }>();
  const activeTab = useMemo<Tab>(() => {
    const value = params.tab as Tab | undefined;
    return value && TAB_KEYS.includes(value) ? value : "agents";
  }, [params.tab]);

  return (
    <WorkspaceSettingsLayout activeTab="publish">
      <Tabs
        activeKey={activeTab}
        onChange={key => navigate(workspaceSettingsPublishPath(workspace.id, key as Tab))}
      >
        <TabPane tab={t("cozeSettingsPublishAgents")} itemKey="agents">
          {activeTab === "agents" ? <AgentsPanel onOpenEditor={agentId => navigate(agentEditorPath(agentId))} /> : null}
        </TabPane>
        <TabPane tab={t("cozeSettingsPublishApps")} itemKey="apps">
          {activeTab === "apps" ? <AppsPanel /> : null}
        </TabPane>
        <TabPane tab={t("cozeSettingsPublishWorkflows")} itemKey="workflows">
          {activeTab === "workflows" ? <WorkflowsPanel onOpenEditor={workflowId => navigate(workflowEditorPath(workflowId))} /> : null}
        </TabPane>
        <TabPane tab={t("cozeSettingsPublishChannels")} itemKey="channels">
          {activeTab === "channels" ? <ChannelsPanel workspaceId={workspace.id} locale={locale} /> : null}
        </TabPane>
      </Tabs>
    </WorkspaceSettingsLayout>
  );
}

function PublishEmpty({ goPath }: { goPath: string }) {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  return (
    <Empty
      title={t("cozeSettingsPublishEmpty")}
      description={(
        <Button theme="solid" type="primary" onClick={() => navigate(goPath)}>
          {t("cozeSettingsPublishGoProjects")}
        </Button>
      )}
    />
  );
}

function AgentsPanel({ onOpenEditor }: { onOpenEditor: (id: string) => void }) {
  const { t } = useAppI18n();
  const workspace = useWorkspaceContext();
  const [items, setItems] = useState<AgentListItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getAiAssistantsPaged({ pageIndex: 1, pageSize: 50, workspaceId: workspace.id })
      .then(result => {
        if (!cancelled) {
          setItems(result.items);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setItems([]);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [workspace.id]);

  const columns: ColumnProps<AgentListItem>[] = [
    { title: t("cozeSettingsPublishColumnName"), dataIndex: "name" },
    {
      title: t("cozeSettingsPublishColumnStatus"),
      dataIndex: "status",
      render: (value: string | undefined) => <Tag color={(value ?? "draft").toLowerCase() === "published" ? "green" : "blue"}>{value ?? "draft"}</Tag>
    },
    { title: t("cozeSettingsPublishColumnUpdatedAt"), dataIndex: "createdAt" },
    {
      title: t("cozeSettingsPublishColumnActions"),
      dataIndex: "id",
      render: (_value, record) => (
        <Button theme="borderless" onClick={() => onOpenEditor(String(record.id))}>{t("cozeSettingsPublishGoProjects")}</Button>
      )
    }
  ];

  if (loading) {
    return <div className="coze-page__loading"><Spin /></div>;
  }
  if (items.length === 0) {
    return <PublishEmpty goPath={workspaceProjectsPath(workspace.id)} />;
  }
  return <Table columns={columns} dataSource={items} rowKey="id" pagination={false} />;
}

function AppsPanel() {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const appGateway = useMemo(() => createLowcodeProjectAppGateway({ navigate }), [navigate]);
  const [items, setItems] = useState<ProjectAppCard[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    appGateway.list({ pageIndex: 1, pageSize: 50, workspaceId: workspace.id })
      .then(result => {
        if (!cancelled) {
          setItems(result.items);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setItems([]);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [appGateway, workspace.id]);

  const columns: ColumnProps<ProjectAppCard>[] = [
    { title: t("cozeSettingsPublishColumnName"), dataIndex: "name" },
    { title: t("cozeSettingsPublishColumnStatus"), dataIndex: "status", render: (value: string) => <Tag color="blue">{value}</Tag> },
    { title: t("cozeSettingsPublishColumnUpdatedAt"), dataIndex: "updatedAt" },
    {
      title: t("cozeSettingsPublishColumnActions"),
      dataIndex: "id",
      render: (_value, record) => (
        <Button theme="borderless" onClick={() => appGateway.open(String(record.id))}>{t("cozeSettingsPublishGoProjects")}</Button>
      )
    }
  ];

  if (loading) {
    return <div className="coze-page__loading"><Spin /></div>;
  }
  if (items.length === 0) {
    return <PublishEmpty goPath={workspaceProjectsPath(workspace.id)} />;
  }
  return <Table columns={columns} dataSource={items} rowKey="id" pagination={false} />;
}

function WorkflowsPanel({ onOpenEditor }: { onOpenEditor: (id: string) => void }) {
  const { t } = useAppI18n();
  const workspace = useWorkspaceContext();
  const [items, setItems] = useState<WorkflowListItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    listWorkflows(1, 100, undefined, workspace.id)
      .then(result => {
        if (!cancelled) {
          const list = (result.data?.items ?? []).filter(item => item.status === 1);
          setItems(list);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setItems([]);
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [workspace.id]);

  const columns: ColumnProps<WorkflowListItem>[] = [
    { title: t("cozeSettingsPublishColumnName"), dataIndex: "name" },
    { title: t("cozeSettingsPublishColumnType"), dataIndex: "mode", render: (value: number) => <Tag color="blue">{value === 1 ? "chatflow" : "workflow"}</Tag> },
    { title: t("cozeSettingsPublishColumnStatus"), dataIndex: "status", render: (value: number) => <Tag color={value === 1 ? "green" : "grey"}>{value === 1 ? "published" : "draft"}</Tag> },
    { title: t("cozeSettingsPublishColumnUpdatedAt"), dataIndex: "publishedAt" },
    {
      title: t("cozeSettingsPublishColumnActions"),
      dataIndex: "id",
      render: (_value, record) => (
        <Button theme="borderless" onClick={() => onOpenEditor(record.id)}>{t("cozeSettingsPublishGoProjects")}</Button>
      )
    }
  ];

  if (loading) {
    return <div className="coze-page__loading"><Spin /></div>;
  }
  if (items.length === 0) {
    return <PublishEmpty goPath={workspaceProjectsPath(workspace.id)} />;
  }
  return <Table columns={columns} dataSource={items} rowKey="id" pagination={false} />;
}

function ChannelsPanel({ workspaceId, locale }: { workspaceId: string; locale: "zh-CN" | "en-US" }) {
  const { t } = useAppI18n();
  const [loadFailed, setLoadFailed] = useState(false);
  const [selectedChannel, setSelectedChannel] = useState<PublishChannelListItem | null>(null);
  const [reloadKey, setReloadKey] = useState(0);
  const [addOpen, setAddOpen] = useState(false);
  const [pendingSelectId, setPendingSelectId] = useState<string | null>(null);

  const refresh = useCallback(() => setReloadKey(value => value + 1), []);
  const loader = useCallback(async (wsId: string): Promise<PublishChannelListItem[]> => {
    try {
      setLoadFailed(false);
      const result = await listWorkspacePublishChannelsPage(wsId, { pageIndex: 1, pageSize: 200 });
      return result.items.map((item) => ({
        id: item.id,
        workspaceId: item.workspaceId,
        type: item.type,
        name: item.name,
        status: item.status,
        authStatus: item.authStatus,
        lastSyncAt: item.lastSyncAt,
        createdAt: item.createdAt,
        updatedAt: item.createdAt
      }));
    } catch (error) {
      setLoadFailed(true);
      throw error;
    }
  }, []);
  const handleLoaded = useCallback((channels: PublishChannelListItem[]) => {
    if (pendingSelectId) {
      const matched = channels.find((item) => item.id === pendingSelectId) ?? null;
      if (matched) {
        setSelectedChannel(matched);
        setPendingSelectId(null);
        return;
      }
    }
    if (!selectedChannel && channels.length > 0) {
      setSelectedChannel(channels[0] ?? null);
    }
  }, [pendingSelectId, selectedChannel]);

  return (
    <>
      {loadFailed ? (
        <Banner
          type="danger"
          bordered
          fullMode={false}
          description={t("cozeSettingsPublishChannelsLoadFailed")}
          style={{ marginBottom: 16 }}
        />
      ) : null}
      <div className="coze-page__toolbar" style={{ display: "flex", gap: 8 }}>
        <Button theme="solid" type="primary" onClick={() => setAddOpen(true)}>
          {t("cozeSettingsChannelAdd")}
        </Button>
        <Button
          disabled={!selectedChannel}
          onClick={() => {
            if (!selectedChannel) return;
            void reauthorizeWorkspacePublishChannel(workspaceId, selectedChannel.id)
              .then(() => {
                Toast.success(t("cozeSettingsChannelReauth"));
                refresh();
              })
              .catch((error) => {
                Toast.error(error instanceof Error ? error.message : t("cozeSettingsPublishChannelsLoadFailed"));
              });
          }}
        >
          {t("cozeSettingsChannelReauth")}
        </Button>
        <Button
          type="danger"
          disabled={!selectedChannel}
          onClick={() => {
            if (!selectedChannel) return;
            void deleteWorkspacePublishChannel(workspaceId, selectedChannel.id)
              .then(() => {
                Toast.success(t("cozeSettingsChannelDelete"));
                setSelectedChannel(null);
                refresh();
              })
              .catch((error) => {
                Toast.error(error instanceof Error ? error.message : t("cozeSettingsPublishChannelsLoadFailed"));
              });
          }}
        >
          {t("cozeSettingsChannelDelete")}
        </Button>
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "minmax(360px, 5fr) minmax(420px, 7fr)", gap: 16 }}>
        <Card bordered title={t("cozeSettingsPublishChannels")}>
          <ChannelsListPanel
            workspaceId={workspaceId}
            locale={locale}
            loader={loader}
            reloadKey={reloadKey}
            selectedChannelId={selectedChannel?.id ?? null}
            onSelect={setSelectedChannel}
            onLoaded={handleLoaded}
          />
        </Card>
        <Card bordered title={t("cozeSettingsChannelEditCredential")}>
          {selectedChannel ? (
            <ChannelDetailRouter
              workspaceId={workspaceId}
              locale={locale}
              channel={selectedChannel}
              releaseLoader={getWorkspaceChannelActiveRelease}
              fetcher={publishChannelsHttpJson}
            />
          ) : (
            <Empty description={t("cozeSettingsPublishEmpty")} />
          )}
        </Card>
      </div>
      <AddChannelModal
        visible={addOpen}
        locale={locale}
        catalogLoader={listPublishChannelCatalog}
        createChannel={(input) => createWorkspacePublishChannel(workspaceId, input)}
        onCancel={() => setAddOpen(false)}
        onCreated={({ channelId }) => {
          setAddOpen(false);
          setPendingSelectId(channelId);
          refresh();
        }}
      />
    </>
  );
}
