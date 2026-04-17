import { useEffect, useMemo, useState } from "react";
import { Avatar, Button, Empty, Input, Select, Spin, TabPane, Tabs, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { IconFolder, IconPlus } from "@douyinfe/semi-icons";
import { useNavigate } from "react-router-dom";
import { agentEditorPath, appEditorPath } from "@atlas/app-shell-shared";
import { useAppI18n } from "../i18n";
import { useWorkspaceContext } from "../workspace-context";
import { CreateAgentModal } from "../components/create-agent-modal";
import { CreateAppModal } from "../components/create-app-modal";
import { CreateFolderModal } from "../components/create-folder-modal";
import { GlobalCreateModal } from "../components/global-create-modal";
import { getAiAssistantsPaged } from "../../services/api-ai-assistant";
import type { AgentListItem } from "../../services/api-agent";
import { getWorkspaceIdeResources } from "../../services/api-workspace-ide";
import type { WorkspaceIdeResourceCardDto } from "../../services/api-workspace-ide";
import { listFolders, type FolderListItem } from "../../services/mock";

type ProjectsTab = "all" | "agents" | "apps" | "folders";

interface UnifiedCard {
  key: string;
  type: "agent" | "app" | "folder";
  name: string;
  description?: string;
  status?: string;
  updatedAt?: string;
  iconLetter: string;
  onOpen: () => void;
}

export function WorkspaceProjectsPage() {
  const { t } = useAppI18n();
  const workspace = useWorkspaceContext();
  const navigate = useNavigate();
  const [tab, setTab] = useState<ProjectsTab>("all");
  const [keyword, setKeyword] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("all");
  const [loading, setLoading] = useState(true);
  const [agents, setAgents] = useState<AgentListItem[]>([]);
  const [apps, setApps] = useState<WorkspaceIdeResourceCardDto[]>([]);
  const [folders, setFolders] = useState<FolderListItem[]>([]);
  const [globalCreateOpen, setGlobalCreateOpen] = useState(false);
  const [createFolderOpen, setCreateFolderOpen] = useState(false);
  const [createAgentOpen, setCreateAgentOpen] = useState(false);
  const [createAppOpen, setCreateAppOpen] = useState(false);

  const refresh = async () => {
    if (!workspace.id) {
      return;
    }
    setLoading(true);
    try {
      const [agentResult, appResult, folderResult] = await Promise.all([
        getAiAssistantsPaged({ pageIndex: 1, pageSize: 50, keyword: keyword.trim() || undefined }).catch(() => ({ items: [], total: 0, pageIndex: 1, pageSize: 50 })),
        getWorkspaceIdeResources({ resourceType: "app", pageIndex: 1, pageSize: 50, keyword: keyword.trim() || undefined }).catch(() => ({ items: [], total: 0, pageIndex: 1, pageSize: 50 })),
        listFolders(workspace.id, { pageIndex: 1, pageSize: 50, keyword: keyword.trim() || undefined }).catch(() => ({ items: [], total: 0, pageIndex: 1, pageSize: 50 }))
      ]);
      setAgents(agentResult.items);
      setApps(appResult.items);
      setFolders(folderResult.items);
    } catch (error) {
      Toast.error((error as Error).message || t("cozeCreateFailed"));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void refresh();
  }, [workspace.id, keyword]);

  const cards = useMemo<UnifiedCard[]>(() => {
    const agentCards: UnifiedCard[] = agents
      .filter(item => filterByStatus(item.status, statusFilter))
      .map(item => ({
        key: `agent-${item.id}`,
        type: "agent",
        name: item.name,
        description: item.description,
        status: item.status,
        iconLetter: (item.name || "A").slice(0, 1).toUpperCase(),
        onOpen: () => navigate(agentEditorPath(String(item.id)))
      }));

    const appCards: UnifiedCard[] = apps
      .filter(item => filterByStatus(item.status, statusFilter))
      .map(item => ({
        key: `app-${item.resourceId}`,
        type: "app",
        name: item.name,
        description: item.description,
        status: item.status,
        updatedAt: item.updatedAt,
        iconLetter: (item.name || "P").slice(0, 1).toUpperCase(),
        onOpen: () => navigate(appEditorPath(String(item.resourceId)))
      }));

    const folderCards: UnifiedCard[] = folders.map(item => ({
      key: `folder-${item.id}`,
      type: "folder",
      name: item.name,
      description: item.description,
      iconLetter: "F",
      onOpen: () => Toast.info(t("cozeCommonComingSoon"))
    }));

    if (tab === "agents") {
      return agentCards;
    }
    if (tab === "apps") {
      return appCards;
    }
    if (tab === "folders") {
      return folderCards;
    }
    return [...folderCards, ...agentCards, ...appCards];
  }, [agents, apps, folders, navigate, statusFilter, t, tab]);

  return (
    <div className="coze-page coze-projects-page" data-testid="coze-projects-page">
      <header className="coze-page__header coze-projects-page__header">
        <div>
          <Typography.Title heading={3} style={{ margin: 0 }}>{t("cozeProjectsTitle")}</Typography.Title>
        </div>
        <div className="coze-projects-page__actions">
          <Input
            value={keyword}
            onChange={value => setKeyword(value)}
            placeholder={t("cozeProjectsSearchPlaceholder")}
            showClear
            style={{ width: 240 }}
          />
          <Button icon={<IconFolder />} onClick={() => setCreateFolderOpen(true)} data-testid="coze-projects-create-folder">
            {t("cozeProjectsCreateFolder")}
          </Button>
          <Button theme="solid" type="primary" icon={<IconPlus />} onClick={() => setGlobalCreateOpen(true)} data-testid="coze-projects-create-project">
            {t("cozeProjectsCreateProject")}
          </Button>
        </div>
      </header>

      <section className="coze-page__toolbar">
        <Select
          style={{ width: 160 }}
          value={statusFilter}
          optionList={[
            { label: t("cozeProjectsFilterStatusAll"), value: "all" },
            { label: t("cozeProjectsFilterStatusDraft"), value: "draft" },
            { label: t("cozeProjectsFilterStatusPublished"), value: "published" },
            { label: t("cozeProjectsFilterStatusArchived"), value: "archived" }
          ]}
          onChange={value => setStatusFilter(String(value))}
        />
      </section>

      <Tabs activeKey={tab} onChange={key => setTab((key as ProjectsTab) ?? "all")}>
        <TabPane tab={t("cozeProjectsTabAll")} itemKey="all" />
        <TabPane tab={t("cozeProjectsTabAgents")} itemKey="agents" />
        <TabPane tab={t("cozeProjectsTabApps")} itemKey="apps" />
        <TabPane tab={t("cozeProjectsTabFolders")} itemKey="folders" />
      </Tabs>

      <section className="coze-page__body">
        {loading ? (
          <div className="coze-page__loading"><Spin /></div>
        ) : cards.length === 0 ? (
          <Empty title={t("cozeProjectsEmptyTitle")} description={t("cozeProjectsEmptyTip")} />
        ) : (
          <div className="coze-card-grid">
            {cards.map(card => (
              <button
                key={card.key}
                type="button"
                className="coze-project-card"
                onClick={card.onOpen}
                data-testid={`coze-project-card-${card.key}`}
              >
                <div className="coze-project-card__head">
                  <Avatar size="small" color="light-blue">{card.iconLetter}</Avatar>
                  <div className="coze-project-card__meta">
                    <strong>{card.name}</strong>
                    <span>{card.description || ""}</span>
                  </div>
                </div>
                <div className="coze-project-card__footer">
                  <Tag size="small">{cardTypeLabel(card.type, t)}</Tag>
                  {card.status ? <Tag size="small" color="blue">{card.status}</Tag> : null}
                </div>
              </button>
            ))}
          </div>
        )}
      </section>

      <GlobalCreateModal
        visible={globalCreateOpen}
        workspaceId={workspace.id}
        onClose={() => {
          setGlobalCreateOpen(false);
          void refresh();
        }}
      />
      <CreateFolderModal
        visible={createFolderOpen}
        workspaceId={workspace.id}
        onClose={() => {
          setCreateFolderOpen(false);
          void refresh();
        }}
      />
      <CreateAgentModal
        visible={createAgentOpen}
        workspaceId={workspace.id}
        onClose={() => {
          setCreateAgentOpen(false);
          void refresh();
        }}
      />
      <CreateAppModal
        visible={createAppOpen}
        workspaceId={workspace.id}
        onClose={() => {
          setCreateAppOpen(false);
          void refresh();
        }}
      />
    </div>
  );
}

function filterByStatus(status: string | undefined, filter: string): boolean {
  if (filter === "all" || !filter) {
    return true;
  }
  return (status ?? "").toLowerCase() === filter;
}

function cardTypeLabel(type: UnifiedCard["type"], t: ReturnType<typeof useAppI18n>["t"]): string {
  if (type === "agent") {
    return t("cozeProjectsFilterTypeAgent");
  }
  if (type === "app") {
    return t("cozeProjectsFilterTypeApp");
  }
  return t("cozeProjectsFilterTypeFolder");
}
