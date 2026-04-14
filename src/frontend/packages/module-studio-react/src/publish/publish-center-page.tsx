import { useEffect, useMemo, useState } from "react";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { Button, Card, Empty, Space, Spin, Table, Tabs, Toast, Typography } from "@douyinfe/semi-ui";
import type { PublishCenterItem, StudioLocale, StudioModuleApi } from "../types";
import { StatusTag } from "../shared/status-tag";
import { ApiAccessPanel } from "./api-access-panel";
import { ChatSdkPanel } from "./chat-sdk-panel";
import { TokenManagement } from "./token-management";

export interface PublishCenterPageProps {
  api: StudioModuleApi;
  locale: StudioLocale;
  /** 用于示例代码中的 API 根路径 */
  apiBase?: string;
  onOpenAgent?: (agentId: string) => void;
  onOpenApp?: (appId: string) => void;
  onOpenWorkflow?: (workflowId: string) => void;
  onOpenPlugin?: (pluginId: string) => void;
  testId?: string;
}

function resourceTitle(locale: StudioLocale, t: PublishCenterItem["resourceType"]): string {
  if (locale === "en-US") {
    const map: Record<PublishCenterItem["resourceType"], string> = {
      agent: "Agents",
      app: "Apps",
      workflow: "Workflows",
      plugin: "Plugins"
    };
    return map[t];
  }
  const map: Record<PublishCenterItem["resourceType"], string> = {
    agent: "智能体",
    app: "应用",
    workflow: "工作流",
    plugin: "插件"
  };
  return map[t];
}

function statusLabel(locale: StudioLocale, status: string): string {
  const normalized = status.toLowerCase();
  if (locale === "en-US") {
    if (normalized === "published") {
      return "Published";
    }
    if (normalized === "outdated") {
      return "Outdated";
    }
    return "Draft";
  }

  if (normalized === "published") {
    return "已发布";
  }
  if (normalized === "outdated") {
    return "有更新";
  }
  return "草稿";
}

function buildColumns(
  locale: StudioLocale,
  handlers: {
    onOpenAgent?: (id: string) => void;
    onOpenApp?: (id: string) => void;
    onOpenWorkflow?: (id: string) => void;
    onOpenPlugin?: (id: string) => void;
  }
): ColumnProps<PublishCenterItem>[] {
  return [
    {
      title: locale === "en-US" ? "Name" : "名称",
      dataIndex: "resourceName",
      render: (value: unknown, record) => (
        <Space vertical align="start" spacing={4}>
          <Typography.Text strong>{String(value ?? "-")}</Typography.Text>
          <Typography.Text type="tertiary" size="small">
            {record.resourceId}
          </Typography.Text>
        </Space>
      )
    },
    {
      title: locale === "en-US" ? "Versions" : "版本",
      key: "versions",
      width: 160,
      render: (_v, record) => (
        <Typography.Text type="tertiary" size="small">
          {locale === "en-US" ? "Published" : "已发布"} v{record.currentVersion}
          {" · "}
          {locale === "en-US" ? "Draft" : "草稿"} v{record.draftVersion}
        </Typography.Text>
      )
    },
    {
      title: locale === "en-US" ? "Status" : "状态",
      dataIndex: "status",
      width: 120,
      render: (value: unknown) => {
        const normalized = String(value ?? "draft");
        return <StatusTag status={normalized} label={statusLabel(locale, normalized)} />;
      }
    },
    {
      title: locale === "en-US" ? "Last published" : "最近发布",
      dataIndex: "lastPublishedAt",
      width: 180,
      render: (value: unknown) => (value ? String(value) : "—")
    },
    {
      title: locale === "en-US" ? "API" : "接口",
      dataIndex: "apiEndpoint",
      render: (value: unknown) =>
        value ? (
          <Typography.Text copyable={{ content: String(value) }} ellipsis={{ showTooltip: true, rows: 2 }}>
            {String(value)}
          </Typography.Text>
        ) : (
          "—"
        )
    },
    {
      title: locale === "en-US" ? "Open" : "打开",
      key: "open",
      width: 120,
      render: (_v, record) => {
        const go =
          record.resourceType === "agent"
            ? handlers.onOpenAgent
            : record.resourceType === "app"
              ? handlers.onOpenApp
              : record.resourceType === "workflow"
                ? handlers.onOpenWorkflow
                : handlers.onOpenPlugin;
        if (!go) {
          return "—";
        }
        return (
          <Button theme="borderless" type="primary" onClick={() => go(record.resourceId)}>
            {locale === "en-US" ? "Open" : "打开"}
          </Button>
        );
      }
    }
  ];
}

export function PublishCenterPage({
  api,
  locale,
  apiBase,
  onOpenAgent,
  onOpenApp,
  onOpenWorkflow,
  onOpenPlugin,
  testId = "studio-publish-center-page"
}: PublishCenterPageProps) {
  const [loading, setLoading] = useState(true);
  const [items, setItems] = useState<PublishCenterItem[]>([]);
  const [filterType, setFilterType] = useState<PublishCenterItem["resourceType"] | "all">("all");

  useEffect(() => {
    let disposed = false;
    setLoading(true);
    void api
      .getPublishCenterItems()
      .then((list) => {
        if (!disposed) {
          setItems(list);
        }
      })
      .catch((error: unknown) => {
        if (!disposed) {
          Toast.error(error instanceof Error ? error.message : locale === "en-US" ? "Failed to load." : "加载失败。");
          setItems([]);
        }
      })
      .finally(() => {
        if (!disposed) {
          setLoading(false);
        }
      });

    return () => {
      disposed = true;
    };
  }, [api, locale]);

  const filteredItems = useMemo(() => {
    if (filterType === "all") {
      return items;
    }
    return items.filter((item) => item.resourceType === filterType);
  }, [items, filterType]);

  const grouped = useMemo(() => {
    const map: Record<PublishCenterItem["resourceType"], PublishCenterItem[]> = {
      agent: [],
      app: [],
      workflow: [],
      plugin: []
    };
    for (const item of items) {
      map[item.resourceType].push(item);
    }
    return map;
  }, [items]);

  const sampleEndpoint = items.find((i) => i.apiEndpoint)?.apiEndpoint;
  const sampleToken = items.find((i) => i.embedToken)?.embedToken;

  const columns = useMemo(
    () => buildColumns(locale, { onOpenAgent, onOpenApp, onOpenWorkflow, onOpenPlugin }),
    [locale, onOpenAgent, onOpenApp, onOpenWorkflow, onOpenPlugin]
  );

  const pageTitle = locale === "en-US" ? "Publish center" : "发布中心";
  const subtitle =
    locale === "en-US"
      ? "Manage published agents, apps, workflows, and plugins from one place."
      : "在同一页面管理已发布的智能体、应用、工作流与插件。";

  return (
    <div className="module-studio__stack" data-testid={testId}>
      <div>
        <Typography.Title heading={3}>{pageTitle}</Typography.Title>
        <Typography.Paragraph type="tertiary" style={{ marginBottom: 16 }}>
          {subtitle}
        </Typography.Paragraph>
      </div>

      <Tabs type="line" keepDOM={false}>
        <Tabs.TabPane tab={locale === "en-US" ? "Catalog" : "发布清单"} itemKey="catalog">
          <Space vertical align="start" style={{ width: "100%" }} spacing={16}>
            <Space wrap>
              {(["all", "agent", "app", "workflow", "plugin"] as const).map((key) => (
                <Button
                  key={key}
                  theme={filterType === key ? "solid" : "light"}
                  type={filterType === key ? "primary" : "tertiary"}
                  onClick={() => setFilterType(key)}
                >
                  {key === "all" ? (locale === "en-US" ? "All" : "全部") : resourceTitle(locale, key)}
                </Button>
              ))}
            </Space>

            {loading ? (
              <Spin />
            ) : filteredItems.length === 0 ? (
              <Empty title={locale === "en-US" ? "No published items" : "暂无发布项"} />
            ) : filterType === "all" ? (
              (["agent", "app", "workflow", "plugin"] as const).map((t) =>
                grouped[t].length === 0 ? null : (
                  <Card key={t} title={resourceTitle(locale, t)} bordered>
                    <Table<PublishCenterItem>
                      rowKey={(r) => (r ? `${r.resourceType}:${r.resourceId}` : "row")}
                      columns={columns}
                      dataSource={grouped[t]}
                      pagination={false}
                      size="small"
                    />
                  </Card>
                )
              )
            ) : (
              <Card title={resourceTitle(locale, filterType)} bordered>
                <Table<PublishCenterItem>
                  rowKey={(r) => (r ? `${r.resourceType}:${r.resourceId}` : "row")}
                  columns={columns}
                  dataSource={filteredItems}
                  pagination={false}
                  size="small"
                />
              </Card>
            )}
          </Space>
        </Tabs.TabPane>

        <Tabs.TabPane tab={locale === "en-US" ? "HTTP / API" : "HTTP 接入"} itemKey="http">
          <ApiAccessPanel locale={locale} apiBase={apiBase} resourcePath={sampleEndpoint ?? "agents/{id}/runtime"} sampleBearerToken={sampleToken ?? "<access_token>"} />
        </Tabs.TabPane>

        <Tabs.TabPane tab={locale === "en-US" ? "Web / React SDK" : "Web / React SDK"} itemKey="sdk">
          <ChatSdkPanel locale={locale} />
        </Tabs.TabPane>

        <Tabs.TabPane tab={locale === "en-US" ? "Embed tokens" : "嵌入令牌"} itemKey="tokens">
          <TokenManagement api={api} locale={locale} items={items} />
        </Tabs.TabPane>
      </Tabs>
    </div>
  );
}
