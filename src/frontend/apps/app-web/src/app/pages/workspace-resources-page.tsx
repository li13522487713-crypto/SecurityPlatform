import { useEffect, useMemo, useState } from "react";
import { Empty, Spin, TabPane, Tabs, Tag, Toast, Typography, Input } from "@douyinfe/semi-ui";
import { useNavigate, useParams } from "react-router-dom";
import {
  chatflowEditorPath,
  workflowEditorPath,
  workspaceResourcesPath,
  type ResourceLeaf
} from "@atlas/app-shell-shared";
import { useAppI18n } from "../i18n";
import { useWorkspaceContext } from "../workspace-context";
import { listWorkflows } from "../../services/api-workflow";
import { getAiPluginsPaged } from "../../services/api-explore";
import { getKnowledgeBasesPaged } from "../../services/api-knowledge";
import { getAiDatabasesPaged } from "../../services/api-ai-database";
import { getAiVariablesPaged } from "../../services/api-ai-variable";
import type { AppMessageKey } from "../messages";

const TAB_KEYS: ResourceLeaf[] = [
  "workflows",
  "chatflows",
  "plugins",
  "knowledge",
  "databases",
  "variables",
  "prompts"
];

interface ResourceRow {
  id: string;
  name: string;
  description?: string;
  meta?: string;
  badge?: string;
  onOpen?: () => void;
}

export function WorkspaceResourcesPage() {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const params = useParams<{ type?: string }>();
  const activeTab = useMemo<ResourceLeaf>(() => {
    const value = params.type as ResourceLeaf | undefined;
    return value && TAB_KEYS.includes(value) ? value : "workflows";
  }, [params.type]);

  const [keyword, setKeyword] = useState("");

  return (
    <div className="coze-page coze-resource-page" data-testid="coze-resource-page">
      <header className="coze-page__header">
        <Typography.Title heading={3} style={{ margin: 0 }}>{t("cozeResourceTitle")}</Typography.Title>
        <Typography.Text type="tertiary">{t("cozeResourceSubtitle")}</Typography.Text>
      </header>

      <section className="coze-page__toolbar">
        <Input
          value={keyword}
          onChange={value => setKeyword(value)}
          placeholder={t("cozeResourceSearchPlaceholder")}
          showClear
          style={{ width: 240 }}
        />
      </section>

      <Tabs
        type="line"
        activeKey={activeTab}
        onChange={key => navigate(workspaceResourcesPath(workspace.id, key as ResourceLeaf))}
      >
        {TAB_KEYS.map(tab => (
          <TabPane key={tab} tab={t(tabLabelKey(tab))} itemKey={tab}>
            {activeTab === tab ? <ResourceTabPanel kind={tab} keyword={keyword} navigate={navigate} /> : null}
          </TabPane>
        ))}
      </Tabs>
    </div>
  );
}

function tabLabelKey(kind: ResourceLeaf): AppMessageKey {
  switch (kind) {
    case "workflows":
      return "cozeResourceTabWorkflows";
    case "chatflows":
      return "cozeResourceTabChatflows";
    case "plugins":
      return "cozeResourceTabPlugins";
    case "knowledge":
      return "cozeResourceTabKnowledge";
    case "databases":
      return "cozeResourceTabDatabases";
    case "variables":
      return "cozeResourceTabVariables";
    case "prompts":
    default:
      return "cozeResourceTabPrompts";
  }
}

function ResourceTabPanel({
  kind,
  keyword,
  navigate
}: {
  kind: ResourceLeaf;
  keyword: string;
  navigate: ReturnType<typeof useNavigate>;
}) {
  const { t } = useAppI18n();
  const workspace = useWorkspaceContext();
  const [rows, setRows] = useState<ResourceRow[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    loadRows(kind, workspace.id, keyword.trim(), navigate)
      .then(result => {
        if (!cancelled) {
          setRows(result);
        }
      })
      .catch(error => {
        if (!cancelled) {
          setRows([]);
          Toast.error((error as Error).message || t("cozeCreateFailed"));
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
  }, [kind, keyword, navigate, t, workspace.id]);

  if (loading) {
    return <div className="coze-page__loading"><Spin /></div>;
  }
  if (rows.length === 0) {
    return <Empty description={t("cozeResourceEmpty")} />;
  }
  return (
    <ul className="coze-list">
      {rows.map(row => (
        <li
          key={row.id}
          className={`coze-list__item${row.onOpen ? " coze-list__item--clickable" : ""}`}
          onClick={() => row.onOpen?.()}
          role={row.onOpen ? "button" : undefined}
        >
          <div>
            <strong>{row.name}</strong>
            <span>{row.description ?? ""}</span>
          </div>
          <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
            {row.badge ? <Tag size="small" color="blue">{row.badge}</Tag> : null}
            {row.meta ? <span style={{ color: "var(--semi-color-text-2)", fontSize: 12 }}>{row.meta}</span> : null}
          </div>
        </li>
      ))}
    </ul>
  );
}

async function loadRows(
  kind: ResourceLeaf,
  workspaceId: string,
  keyword: string,
  navigate: ReturnType<typeof useNavigate>
): Promise<ResourceRow[]> {
  if (kind === "workflows" || kind === "chatflows") {
    const response = await listWorkflows(1, 100, keyword || undefined, workspaceId);
    const wantedMode = kind === "chatflows" ? 1 : 0;
    return (response.data?.items ?? [])
      .filter(item => (item.mode ?? 0) === wantedMode)
      .map(item => ({
        id: item.id,
        name: item.name,
        description: item.description,
        meta: item.updatedAt,
        badge: item.status === 1 ? "published" : "draft",
        onOpen: () =>
          navigate(item.mode === 1 ? chatflowEditorPath(item.id) : workflowEditorPath(item.id))
      }));
  }

  if (kind === "plugins") {
    const result = await getAiPluginsPaged({ pageIndex: 1, pageSize: 50 }, keyword || undefined);
    return result.items.map(item => ({
      id: String(item.id),
      name: item.name,
      description: item.description,
      meta: item.category,
      badge: String(item.type)
    }));
  }

  if (kind === "knowledge") {
    const result = await getKnowledgeBasesPaged({ pageIndex: 1, pageSize: 50 }, keyword || undefined);
    return result.items.map(item => ({
      id: String(item.id),
      name: item.name,
      description: item.description,
      meta: `${item.documentCount} docs`,
      badge: String(item.type)
    }));
  }

  if (kind === "databases") {
    const result = await getAiDatabasesPaged({ pageIndex: 1, pageSize: 50 }, keyword || undefined);
    return result.items.map(item => ({
      id: String(item.id),
      name: item.name,
      description: item.description,
      meta: `${item.recordCount} rows`
    }));
  }

  if (kind === "variables") {
    const result = await getAiVariablesPaged({ pageIndex: 1, pageSize: 50 }, { keyword: keyword || undefined });
    return result.items.map(item => ({
      id: String(item.id),
      name: item.key,
      description: item.value,
      meta: item.updatedAt
    }));
  }

  // prompts：暂无真实接口，返回空数组（Empty 组件会展示提示）。
  return [];
}
