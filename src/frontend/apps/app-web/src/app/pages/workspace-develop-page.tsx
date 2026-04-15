import { useDeferredValue, useEffect, useMemo, useState } from "react";
import { Button, Empty, Input, Modal, Tag, TextArea, Typography } from "@douyinfe/semi-ui";
import type {
  WorkspaceAppCardDto,
  WorkspaceResourceCardDto
} from "@/services/api-org-workspaces";
import { useAppI18n } from "../i18n";

type WorkspaceSecondaryTab = "agents" | "workflow" | "chatflow" | "plugins" | "knowledge-base" | "database";

interface WorkspaceDevelopPageProps {
  loading: boolean;
  creating: boolean;
  workspaceName: string;
  keyword: string;
  appItems: WorkspaceAppCardDto[];
  secondaryLoading: boolean;
  secondaryItems: WorkspaceResourceCardDto[];
  secondaryTab: WorkspaceSecondaryTab;
  onKeywordChange: (value: string) => void;
  onSecondaryTabChange: (tab: WorkspaceSecondaryTab) => void;
  onOpenApp: (appId: string) => void;
  onOpenAppPublish: (appId: string) => void;
  onOpenAppWorkflow: (appId: string, workflowId: string) => void;
  onOpenResource: (resource: WorkspaceResourceCardDto) => void;
  onCreateApp: (request: { name: string; description?: string }) => Promise<void>;
  onCreateAgent: (request: { name: string; description?: string }) => Promise<void>;
  onCreatePlugin: (request: { name: string; description?: string; category?: string }) => Promise<void>;
  onCreateDatabase: (request: { name: string; description?: string; tableSchema: string }) => Promise<void>;
}

export function WorkspaceDevelopPage({
  loading,
  creating,
  workspaceName,
  keyword,
  appItems,
  secondaryLoading,
  secondaryItems,
  secondaryTab,
  onKeywordChange,
  onSecondaryTabChange,
  onOpenApp,
  onOpenAppPublish,
  onOpenAppWorkflow,
  onOpenResource,
  onCreateApp,
  onCreateAgent,
  onCreatePlugin,
  onCreateDatabase
}: WorkspaceDevelopPageProps) {
  const { t } = useAppI18n();
  const deferredKeyword = useDeferredValue(keyword.trim().toLowerCase());
  const [createVisible, setCreateVisible] = useState(false);
  const [draftName, setDraftName] = useState("");
  const [draftDescription, setDraftDescription] = useState("");
  const [secondaryCreateVisible, setSecondaryCreateVisible] = useState(false);
  const [secondaryName, setSecondaryName] = useState("");
  const [secondaryDescription, setSecondaryDescription] = useState("");
  const [secondaryCategory, setSecondaryCategory] = useState("");
  const [secondarySchema, setSecondarySchema] = useState('[{"name":"id"},{"name":"title"},{"name":"content"}]');
  const [selectedAppId, setSelectedAppId] = useState<string>("");

  const filteredApps = useMemo(() => {
    if (!deferredKeyword) {
      return appItems;
    }

    return appItems.filter(item => {
      const haystack = `${item.name} ${item.description ?? ""}`.toLowerCase();
      return haystack.includes(deferredKeyword);
    });
  }, [appItems, deferredKeyword]);

  useEffect(() => {
    if (!selectedAppId && filteredApps[0]) {
      setSelectedAppId(filteredApps[0].appId);
    }
  }, [filteredApps, selectedAppId]);

  const selectedApp = useMemo(
    () => filteredApps.find(item => item.appId === selectedAppId) ?? filteredApps[0] ?? null,
    [filteredApps, selectedAppId]
  );

  const tabOptions: Array<{ key: WorkspaceSecondaryTab; label: string }> = [
    { key: "agents", label: t("workspaceDevelopTabAgents") },
    { key: "workflow", label: t("workspaceDevelopTabWorkflow") },
    { key: "chatflow", label: t("workspaceDevelopTabChatflow") },
    { key: "plugins", label: t("workspaceDevelopTabPlugins") },
    { key: "knowledge-base", label: t("workspaceDevelopTabKnowledge") },
    { key: "database", label: t("workspaceDevelopTabDatabases") }
  ];

  const secondaryCreateLabel = secondaryTab === "agents"
    ? t("workspaceDevelopCreateAgent")
    : secondaryTab === "plugins"
      ? t("workspaceDevelopCreatePlugin")
      : secondaryTab === "database"
        ? t("workspaceDevelopCreateDatabase")
        : "";

  const supportsSecondaryCreate = secondaryTab === "agents" || secondaryTab === "plugins" || secondaryTab === "database";

  return (
    <div className="atlas-develop-page" data-testid="workspace-develop-page">
      <section className="atlas-develop-hero">
        <div className="atlas-develop-hero__copy">
          <span className="atlas-develop-hero__kicker">{t("workspaceDevelopKicker")}</span>
          <Typography.Title heading={2} style={{ margin: 0 }}>
            {t("workspaceDevelopTitle")}
          </Typography.Title>
          <Typography.Text type="tertiary">
            {t("workspaceDevelopSubtitle").replace("{workspace}", workspaceName)}
          </Typography.Text>
        </div>

        <div className="atlas-develop-hero__actions">
          <Input
            value={keyword}
            onChange={onKeywordChange}
            showClear
            placeholder={t("workspaceDevelopSearchPlaceholder")}
            data-testid="workspace-develop-search"
          />
          <Button type="primary" theme="solid" loading={creating} onClick={() => setCreateVisible(true)}>
            {t("workspaceDevelopCreateApp")}
          </Button>
        </div>
      </section>

      <div className="atlas-develop-layout">
        <section className="atlas-develop-main">
          <div className="atlas-develop-section-head">
            <div>
              <Typography.Title heading={5} style={{ margin: 0 }}>
                {t("workspaceDevelopAppsTitle")}
              </Typography.Title>
              <Typography.Text type="tertiary">
                {t("workspaceDevelopAppsSubtitle")}
              </Typography.Text>
            </div>
          </div>

          {loading ? (
            <div className="atlas-develop-empty">
              <Typography.Text type="tertiary">{t("loading")}</Typography.Text>
            </div>
          ) : filteredApps.length === 0 ? (
            <div className="atlas-develop-empty">
              <Empty description={t("workspaceDevelopAppsEmpty")} />
            </div>
          ) : (
            <div className="atlas-develop-app-grid">
              {filteredApps.map(item => (
                <article
                  key={item.appId}
                  className={`atlas-develop-app-card${selectedApp?.appId === item.appId ? " is-active" : ""}`}
                  onClick={() => setSelectedAppId(item.appId)}
                >
                  <div className="atlas-develop-app-card__head">
                    <div>
                      <Tag color="blue">{t("workspaceDevelopAppTag")}</Tag>
                      <Typography.Title heading={6} style={{ margin: "10px 0 0" }}>
                        {item.name}
                      </Typography.Title>
                    </div>
                    <Tag color={item.publishStatus === "published" ? "green" : "light-blue"}>
                      {item.publishStatus}
                    </Tag>
                  </div>

                  <Typography.Text type="tertiary">
                    {item.description || t("workspaceDevelopAppDescriptionFallback")}
                  </Typography.Text>

                  <div className="atlas-develop-app-card__actions">
                    <Button theme="light" onClick={(event) => { event.stopPropagation(); onOpenApp(item.appId); }}>
                      {t("workspaceDevelopOpenDetail")}
                    </Button>
                    <Button theme="light" onClick={(event) => { event.stopPropagation(); onOpenAppPublish(item.appId); }}>
                      {t("workspaceDevelopOpenPublish")}
                    </Button>
                    {item.workflowId ? (
                      <Button
                        type="primary"
                        theme="solid"
                        onClick={(event) => {
                          event.stopPropagation();
                          onOpenAppWorkflow(item.appId, item.workflowId!);
                        }}
                      >
                        {t("workspaceDevelopOpenWorkflow")}
                      </Button>
                    ) : null}
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>

        <aside className="atlas-develop-side">
          <section className="atlas-develop-panel">
            <div className="atlas-develop-section-head">
              <div>
                <Typography.Title heading={6} style={{ margin: 0 }}>
                  {t("workspaceDevelopSelectedTitle")}
                </Typography.Title>
                <Typography.Text type="tertiary">
                  {t("workspaceDevelopSelectedSubtitle")}
                </Typography.Text>
              </div>
            </div>

            {selectedApp ? (
              <div className="atlas-develop-selected">
                <Tag color="blue">{selectedApp.status}</Tag>
                <Typography.Title heading={5} style={{ margin: 0 }}>
                  {selectedApp.name}
                </Typography.Title>
                <Typography.Text type="tertiary">
                  {selectedApp.description || t("workspaceDevelopAppDescriptionFallback")}
                </Typography.Text>
                <div className="atlas-develop-selected__actions">
                  <Button type="primary" theme="solid" onClick={() => onOpenApp(selectedApp.appId)}>
                    {t("workspaceDevelopOpenDetail")}
                  </Button>
                  <Button onClick={() => onOpenAppPublish(selectedApp.appId)}>
                    {t("workspaceDevelopOpenPublish")}
                  </Button>
                  {selectedApp.workflowId ? (
                    <Button onClick={() => onOpenAppWorkflow(selectedApp.appId, selectedApp.workflowId!)}>
                      {t("workspaceDevelopOpenWorkflow")}
                    </Button>
                  ) : null}
                </div>
              </div>
            ) : (
              <Empty description={t("workspaceDevelopSelectedEmpty")} />
            )}
          </section>

          <section className="atlas-develop-panel">
            <div className="atlas-develop-section-head">
              <div>
                <Typography.Title heading={6} style={{ margin: 0 }}>
                  {t("workspaceDevelopResourcesTitle")}
                </Typography.Title>
                <Typography.Text type="tertiary">
                  {t("workspaceDevelopResourcesSubtitle")}
                </Typography.Text>
              </div>
              {supportsSecondaryCreate ? (
                <Button theme="light" onClick={() => setSecondaryCreateVisible(true)}>
                  {secondaryCreateLabel}
                </Button>
              ) : null}
            </div>

            <div className="atlas-develop-tabs">
              {tabOptions.map(option => (
                <button
                  key={option.key}
                  type="button"
                  className={`atlas-develop-tab${secondaryTab === option.key ? " is-active" : ""}`}
                  onClick={() => onSecondaryTabChange(option.key)}
                >
                  {option.label}
                </button>
              ))}
            </div>

            {secondaryLoading ? (
              <div className="atlas-develop-empty">
                <Typography.Text type="tertiary">{t("loading")}</Typography.Text>
              </div>
            ) : secondaryItems.length === 0 ? (
              <div className="atlas-develop-empty">
                <Empty description={t("workspaceDevelopResourcesEmpty")} />
              </div>
            ) : (
              <div className="atlas-develop-resource-list">
                {secondaryItems.map(item => (
                  <button
                    key={`${item.resourceType}-${item.resourceId}`}
                    type="button"
                    className="atlas-develop-resource-item"
                    onClick={() => onOpenResource(item)}
                  >
                    <div>
                      <strong>{item.name}</strong>
                      <span>{item.description || item.resourceType}</span>
                    </div>
                    <Tag color="light-blue">{item.publishStatus}</Tag>
                  </button>
                ))}
              </div>
            )}
          </section>
        </aside>
      </div>

      <Modal
        title={t("workspaceDevelopCreateDialogTitle")}
        visible={createVisible}
        onCancel={() => setCreateVisible(false)}
        onOk={() => {
          void onCreateApp({
            name: draftName.trim(),
            description: draftDescription.trim() || undefined
          }).then(() => {
            setCreateVisible(false);
            setDraftName("");
            setDraftDescription("");
          });
        }}
        okButtonProps={{ disabled: !draftName.trim(), loading: creating }}
      >
        <div className="atlas-develop-dialog">
          <Input
            value={draftName}
            onChange={setDraftName}
            placeholder={t("workspaceDevelopCreateNamePlaceholder")}
          />
          <Input
            value={draftDescription}
            onChange={setDraftDescription}
            placeholder={t("workspaceDevelopCreateDescriptionPlaceholder")}
          />
        </div>
      </Modal>

      <Modal
        title={secondaryCreateLabel}
        visible={secondaryCreateVisible}
        onCancel={() => setSecondaryCreateVisible(false)}
        onOk={() => {
          const run = secondaryTab === "agents"
            ? onCreateAgent({
                name: secondaryName.trim(),
                description: secondaryDescription.trim() || undefined
              })
            : secondaryTab === "plugins"
              ? onCreatePlugin({
                  name: secondaryName.trim(),
                  description: secondaryDescription.trim() || undefined,
                  category: secondaryCategory.trim() || undefined
                })
              : onCreateDatabase({
                  name: secondaryName.trim(),
                  description: secondaryDescription.trim() || undefined,
                  tableSchema: secondarySchema.trim()
                });

          void run.then(() => {
            setSecondaryCreateVisible(false);
            setSecondaryName("");
            setSecondaryDescription("");
            setSecondaryCategory("");
            setSecondarySchema('[{"name":"id"},{"name":"title"},{"name":"content"}]');
          });
        }}
        okButtonProps={{ disabled: !secondaryName.trim() || (secondaryTab === "database" && !secondarySchema.trim()) }}
      >
        <div className="atlas-develop-dialog">
          <Input
            value={secondaryName}
            onChange={setSecondaryName}
            placeholder={t("workspaceDevelopSecondaryNamePlaceholder")}
          />
          <Input
            value={secondaryDescription}
            onChange={setSecondaryDescription}
            placeholder={t("workspaceDevelopSecondaryDescriptionPlaceholder")}
          />
          {secondaryTab === "plugins" ? (
            <Input
              value={secondaryCategory}
              onChange={setSecondaryCategory}
              placeholder={t("workspaceDevelopPluginCategoryPlaceholder")}
            />
          ) : null}
          {secondaryTab === "database" ? (
            <TextArea
              autosize
              value={secondarySchema}
              onChange={setSecondarySchema}
              placeholder={t("workspaceDevelopDatabaseSchemaPlaceholder")}
            />
          ) : null}
        </div>
      </Modal>
    </div>
  );
}
