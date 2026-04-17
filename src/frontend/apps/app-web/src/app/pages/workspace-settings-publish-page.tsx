import { useEffect, useMemo, useState } from "react";
import { Button, Empty, Spin, Table, TabPane, Tabs, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { useNavigate, useParams } from "react-router-dom";
import {
  agentEditorPath,
  appEditorPath,
  workflowEditorPath,
  workspaceProjectsPath,
  workspaceSettingsPublishPath,
  type WorkspaceSettingsPublishTab
} from "@atlas/app-shell-shared";
import { useAppI18n } from "../i18n";
import { useWorkspaceContext } from "../workspace-context";
import { getAiAssistantsPaged } from "../../services/api-ai-assistant";
import type { AgentListItem } from "../../services/api-agent";
import {
  getWorkspaceIdeResources,
  type WorkspaceIdeResourceCardDto
} from "../../services/api-workspace-ide";
import { listWorkflows, type WorkflowListItem } from "../../services/api-workflow";
import {
  deletePublishChannel,
  listPublishChannels,
  reauthPublishChannel,
  type PublishChannelItem
} from "../../services/mock";

type Tab = WorkspaceSettingsPublishTab;

const TAB_KEYS: Tab[] = ["agents", "apps", "workflows", "channels"];

export function WorkspaceSettingsPublishPage() {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const params = useParams<{ tab?: string }>();
  const activeTab = useMemo<Tab>(() => {
    const value = params.tab as Tab | undefined;
    return value && TAB_KEYS.includes(value) ? value : "agents";
  }, [params.tab]);

  const subtitle = useMemo(
    () => t("cozeSettingsSubtitle").replace("{workspace}", workspace.name || workspace.appKey || ""),
    [t, workspace.appKey, workspace.name]
  );

  return (
    <div className="coze-page coze-settings-page" data-testid="coze-settings-publish-page">
      <header className="coze-page__header">
        <Typography.Text type="tertiary">{t("cozeSettingsKicker")}</Typography.Text>
        <Typography.Title heading={3} style={{ margin: "8px 0 4px" }}>{t("cozeMenuSettings")}</Typography.Title>
        <Typography.Text type="tertiary">{subtitle}</Typography.Text>
      </header>

      <Tabs
        activeKey={activeTab}
        onChange={key => navigate(workspaceSettingsPublishPath(workspace.id, key as Tab))}
      >
        <TabPane tab={t("cozeSettingsPublishAgents")} itemKey="agents">
          {activeTab === "agents" ? <AgentsPanel onOpenEditor={agentId => navigate(agentEditorPath(agentId))} /> : null}
        </TabPane>
        <TabPane tab={t("cozeSettingsPublishApps")} itemKey="apps">
          {activeTab === "apps" ? <AppsPanel onOpenEditor={appId => navigate(appEditorPath(appId))} /> : null}
        </TabPane>
        <TabPane tab={t("cozeSettingsPublishWorkflows")} itemKey="workflows">
          {activeTab === "workflows" ? <WorkflowsPanel onOpenEditor={workflowId => navigate(workflowEditorPath(workflowId))} /> : null}
        </TabPane>
        <TabPane tab={t("cozeSettingsPublishChannels")} itemKey="channels">
          {activeTab === "channels" ? <ChannelsPanel workspaceId={workspace.id} /> : null}
        </TabPane>
      </Tabs>
    </div>
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
    getAiAssistantsPaged({ pageIndex: 1, pageSize: 50 })
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
  }, []);

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

function AppsPanel({ onOpenEditor }: { onOpenEditor: (id: string) => void }) {
  const { t } = useAppI18n();
  const workspace = useWorkspaceContext();
  const [items, setItems] = useState<WorkspaceIdeResourceCardDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getWorkspaceIdeResources({ resourceType: "app", pageIndex: 1, pageSize: 50 })
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
  }, []);

  const columns: ColumnProps<WorkspaceIdeResourceCardDto>[] = [
    { title: t("cozeSettingsPublishColumnName"), dataIndex: "name" },
    { title: t("cozeSettingsPublishColumnStatus"), dataIndex: "publishStatus", render: (value: string) => <Tag color="blue">{value}</Tag> },
    { title: t("cozeSettingsPublishColumnUpdatedAt"), dataIndex: "updatedAt" },
    {
      title: t("cozeSettingsPublishColumnActions"),
      dataIndex: "resourceId",
      render: (_value, record) => (
        <Button theme="borderless" onClick={() => onOpenEditor(String(record.resourceId))}>{t("cozeSettingsPublishGoProjects")}</Button>
      )
    }
  ];

  if (loading) {
    return <div className="coze-page__loading"><Spin /></div>;
  }
  if (items.length === 0) {
    return <PublishEmpty goPath={workspaceProjectsPath(workspace.id)} />;
  }
  return <Table columns={columns} dataSource={items} rowKey="resourceId" pagination={false} />;
}

function WorkflowsPanel({ onOpenEditor }: { onOpenEditor: (id: string) => void }) {
  const { t } = useAppI18n();
  const workspace = useWorkspaceContext();
  const [items, setItems] = useState<WorkflowListItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    listWorkflows(1, 100)
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
  }, []);

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

function ChannelsPanel({ workspaceId }: { workspaceId: string }) {
  const { t } = useAppI18n();
  const [items, setItems] = useState<PublishChannelItem[]>([]);
  const [loading, setLoading] = useState(true);

  const refresh = () => {
    setLoading(true);
    listPublishChannels(workspaceId, { pageIndex: 1, pageSize: 50 })
      .then(result => setItems(result.items))
      .catch(() => setItems([]))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    refresh();
  }, [workspaceId]);

  const columns: ColumnProps<PublishChannelItem>[] = [
    { title: t("cozeSettingsPublishColumnName"), dataIndex: "name" },
    { title: t("cozeSettingsPublishColumnType"), dataIndex: "type" },
    {
      title: t("cozeSettingsPublishColumnStatus"),
      dataIndex: "status",
      render: (value: PublishChannelItem["status"]) => (
        <Tag color={value === "active" ? "green" : value === "pending" ? "amber" : "grey"}>
          {value === "active" ? t("cozeSettingsChannelStatusActive") : value === "pending" ? t("cozeSettingsChannelStatusPending") : t("cozeSettingsChannelStatusInactive")}
        </Tag>
      )
    },
    {
      title: t("cozeSettingsChannelLastSync").replace("{time}", ""),
      dataIndex: "lastSyncAt"
    },
    {
      title: t("cozeSettingsPublishColumnActions"),
      dataIndex: "id",
      render: (_value, record) => (
        <span style={{ display: "flex", gap: 8 }}>
          <Button
            theme="borderless"
            onClick={() => {
              void reauthPublishChannel(workspaceId, String(record.id)).then(() => {
                Toast.success(t("cozeSettingsChannelReauth"));
                refresh();
              });
            }}
          >
            {t("cozeSettingsChannelReauth")}
          </Button>
          <Button
            theme="borderless"
            type="danger"
            onClick={() => {
              void deletePublishChannel(workspaceId, String(record.id)).then(() => {
                Toast.success(t("cozeSettingsChannelDelete"));
                refresh();
              });
            }}
          >
            {t("cozeSettingsChannelDelete")}
          </Button>
        </span>
      )
    }
  ];

  if (loading) {
    return <div className="coze-page__loading"><Spin /></div>;
  }
  return (
    <>
      <div className="coze-page__toolbar">
        <Button theme="solid" type="primary" onClick={() => Toast.info(t("cozeCommonComingSoon"))}>
          {t("cozeSettingsChannelAdd")}
        </Button>
      </div>
      {items.length === 0 ? (
        <Empty description={t("cozeSettingsPublishEmpty")} />
      ) : (
        <Table columns={columns} dataSource={items} rowKey="id" pagination={false} />
      )}
    </>
  );
}
