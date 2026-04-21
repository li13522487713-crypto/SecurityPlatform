import { useDeferredValue, useEffect, useMemo, useRef, useState } from "react";
import {
  Avatar,
  Button,
  Dropdown,
  Empty,
  Form,
  Input,
  Modal,
  Skeleton,
  Spin,
  TabPane,
  Tag,
  Tabs,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import {
  IconEdit,
  IconMore,
  IconPlus,
  IconSearch,
  IconDelete
} from "@douyinfe/semi-icons";
import { useNavigate, useSearchParams } from "react-router-dom";
import type {
  WorkspaceAppInstanceCreateRequest,
  WorkspaceAppInstanceDto,
  WorkspaceCreateRequest,
  WorkspaceSummaryDto,
  WorkspaceUpdateRequest
} from "../../services/api-org-workspaces";
import {
  getHomeAnnouncements,
  getHomeRecentActivities,
  getHomeRecommendedAgents,
  getHomeTutorials,
  type AnnouncementItem,
  type AnnouncementTab,
  type RecentActivityItem,
  type RecommendedAgentItem,
  type TutorialCard
} from "../../services/mock";
import { useAppI18n } from "../i18n";

const { Title, Text } = Typography;

function getAvatarColor(name: string): string {
  const palette = [
    "#6366f1", "#8b5cf6", "#ec4899", "#f43f5e",
    "#f97316", "#eab308", "#22c55e", "#14b8a6", "#3b82f6"
  ];
  let hash = 0;
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  }
  return palette[Math.abs(hash) % palette.length];
}

function WorkspaceAvatar({ name, size = 40 }: { name: string; size?: number }) {
  const letter = (name || "W").slice(0, 1).toUpperCase();
  const bg = getAvatarColor(name || "W");
  return (
    <div
      style={{
        width: size,
        height: size,
        borderRadius: size / 4,
        background: bg,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        flexShrink: 0,
        color: "#fff",
        fontWeight: 700,
        fontSize: size * 0.42
      }}
    >
      {letter}
    </div>
  );
}

interface WorkspaceCardProps {
  item: WorkspaceSummaryDto;
  deleting: boolean;
  onOpen: () => void;
  onEdit: () => void;
  onDelete: () => void;
}

function WorkspaceCard({ item, deleting, onOpen, onEdit, onDelete }: WorkspaceCardProps) {
  const { t } = useAppI18n();
  const [menuVisible, setMenuVisible] = useState(false);

  const menuItems = [
    {
      node: "item" as const,
      key: "edit",
      name: (
        <span style={{ display: "flex", alignItems: "center", gap: 6 }}>
          <IconEdit size="small" />
          {t("workspaceListEdit")}
        </span>
      ),
      onClick: () => { setMenuVisible(false); onEdit(); }
    },
    {
      node: "item" as const,
      key: "delete",
      type: "danger" as const,
      name: (
      <span style={{ display: "flex", alignItems: "center", gap: 6, color: "#f43f5e" }}>
            <IconDelete size="small" />
            {t("workspaceListArchive")}
          </span>
      ),
      onClick: () => { setMenuVisible(false); }
    }
  ];

  return (
    <div
      className="atlas-workspace-card"
      data-testid={`workspace-card-${item.id}`}
      style={{
        background: "#fff",
        border: "1px solid #f0f0f0",
        borderRadius: 16,
        padding: "16px",
        display: "flex",
        flexDirection: "column",
        gap: 10,
        cursor: "pointer",
        transition: "box-shadow 0.2s, border-color 0.2s",
        position: "relative",
        boxShadow: "0 1px 4px rgba(0,0,0,0.06)",
        minHeight: 176
      }}
      onMouseEnter={e => {
        (e.currentTarget as HTMLElement).style.boxShadow = "0 4px 16px rgba(0,0,0,0.10)";
        (e.currentTarget as HTMLElement).style.borderColor = "#d0d0d0";
      }}
      onMouseLeave={e => {
        (e.currentTarget as HTMLElement).style.boxShadow = "0 1px 4px rgba(0,0,0,0.06)";
        (e.currentTarget as HTMLElement).style.borderColor = "#f0f0f0";
      }}
      onClick={onOpen}
    >
      {/* More menu button */}
      <div
        style={{ position: "absolute", top: 12, right: 12 }}
        onClick={e => e.stopPropagation()}
      >
        <Dropdown
          visible={menuVisible}
          onVisibleChange={setMenuVisible}
          trigger="click"
          position="bottomRight"
          menu={menuItems}
        >
          <button
            type="button"
            data-testid={`workspace-card-menu-${item.id}`}
            style={{
              background: "transparent",
              border: "none",
              cursor: "pointer",
              padding: "4px 6px",
              borderRadius: 8,
              color: "#9ca3af",
              display: "flex",
              alignItems: "center"
            }}
            onMouseEnter={e => { (e.currentTarget as HTMLElement).style.background = "#f3f4f6"; }}
            onMouseLeave={e => { (e.currentTarget as HTMLElement).style.background = "transparent"; }}
          >
            <IconMore />
          </button>
        </Dropdown>
      </div>

      {/* Avatar + name */}
      <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
        <WorkspaceAvatar name={item.name || item.appKey} size={44} />
        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{
            fontWeight: 600,
            fontSize: 14,
            color: "#1f2329",
            overflow: "hidden",
            textOverflow: "ellipsis",
            whiteSpace: "nowrap",
            paddingRight: 24
          }}>
            {item.name || item.appKey}
          </div>
          <div style={{
              fontSize: 11,
              color: "#6b7280",
              overflow: "hidden",
              textOverflow: "ellipsis",
              display: "-webkit-box",
              WebkitLineClamp: 2,
              WebkitBoxOrient: "vertical" as const,
              lineHeight: 1.35,
              minHeight: 30,
              marginTop: 2
            }}>
              {item.description || t("workspaceListDescriptionFallback")}
            </div>
        </div>
      </div>

      {/* Stats */}
      <div style={{ display: "flex", gap: 16 }}>
        <div style={{ display: "flex", flexDirection: "column", alignItems: "center" }}>
          <span style={{ fontSize: 16, fontWeight: 700, color: "#1f2329" }}>{item.appCount ?? 0}</span>
          <span style={{ fontSize: 11, color: "#9ca3af" }}>{t("workspaceListAppCount")}</span>
        </div>
        <div style={{ display: "flex", flexDirection: "column", alignItems: "center" }}>
          <span style={{ fontSize: 16, fontWeight: 700, color: "#1f2329" }}>{item.agentCount ?? 0}</span>
          <span style={{ fontSize: 11, color: "#9ca3af" }}>{t("workspaceListAgentCount")}</span>
        </div>
        <div style={{ display: "flex", flexDirection: "column", alignItems: "center" }}>
          <span style={{ fontSize: 16, fontWeight: 700, color: "#1f2329" }}>{item.workflowCount ?? 0}</span>
          <span style={{ fontSize: 11, color: "#9ca3af" }}>{t("workspaceListWorkflowCount")}</span>
        </div>
      </div>

      {/* Footer: role */}
      <div style={{ display: "flex", alignItems: "center", gap: 8, marginTop: 4 }}>
        <Tag size="small" color="blue" style={{ borderRadius: 6 }}>
          {item.roleCode === "Owner" ? "Owner" : item.roleCode === "Admin" ? "Admin" : "Member"}
        </Tag>
        <Button
          type="tertiary"
          theme="borderless"
          size="small"
          data-testid={`workspace-open-${item.id}`}
          style={{ marginLeft: "auto" }}
          onClick={(event) => {
            event.stopPropagation();
            onOpen();
          }}
        >
          {t("workspaceListOpen")}
        </Button>
        {deleting ? <Spin size="small" /> : null}
      </div>
    </div>
  );
}

interface WorkspaceFormModalProps {
  visible: boolean;
  saving: boolean;
  initialValues?: { name: string; description?: string };
  title: string;
  okText: string;
  onOk: (values: { name: string; description?: string }) => Promise<void>;
  onCancel: () => void;
}

function WorkspaceFormModal({
  visible,
  saving,
  initialValues,
  title,
  okText,
  onOk,
  onCancel
}: WorkspaceFormModalProps) {
  const { t } = useAppI18n();
  const [name, setName] = useState(initialValues?.name ?? "");
  const [description, setDescription] = useState(initialValues?.description ?? "");
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (visible) {
      setName(initialValues?.name ?? "");
      setDescription(initialValues?.description ?? "");
      setTimeout(() => inputRef.current?.focus(), 80);
    }
  }, [visible, initialValues?.name, initialValues?.description]);
  const handleOk = async () => {
    const trimmed = name.trim();
    if (!trimmed) {
      Toast.warning(t("workspaceListNamePlaceholder"));
      return;
    }
    await onOk({ name: trimmed, description: description.trim() || undefined });
  };

  return (
    <Modal
      title={title}
      visible={visible}
      data-testid="workspace-form-modal"
      okButtonProps={{ "data-testid": "workspace-form-submit" }}
      cancelButtonProps={{ "data-testid": "workspace-form-cancel" }}
      onOk={() => { void handleOk(); }}
      onCancel={onCancel}
      confirmLoading={saving}
      okText={okText}
      cancelText={t("cancel")}
      closeOnEsc
      style={{ width: 460 }}
    >
      <Form layout="vertical" style={{ marginTop: 8 }}>
        <Form.Label required>{t("workspaceListCreateNameLabel")}</Form.Label>
        <Input
          data-testid="workspace-form-name"
          ref={inputRef as React.Ref<HTMLInputElement>}
          value={name}
          onChange={setName}
          placeholder={t("workspaceListNamePlaceholder")}
          maxLength={64}
          showClear
          size="large"
          style={{ marginBottom: 16 }}
        />
        <Form.Label>{t("workspaceListCreateDescriptionLabel")}</Form.Label>
        <Input
          data-testid="workspace-form-description"
          value={description}
          onChange={setDescription}
          placeholder={t("workspaceListDescriptionPlaceholder")}
          maxLength={200}
          showClear
          size="large"
        />
      </Form>
    </Modal>
  );
}

interface OrganizationWorkspacesPageProps {
  loading: boolean;
  canManage: boolean;
  saving: boolean;
  deletingWorkspaceId: string | null;
  keyword: string;
  items: WorkspaceSummaryDto[];
  activeWorkspaceId: string;
  activeWorkspaceLabel: string;
  onKeywordChange: (value: string) => void;
  onOpenWorkspace: (workspaceId: string) => void;
  onCreateWorkspace: (request: WorkspaceCreateRequest) => Promise<string>;
  onUpdateWorkspace: (workspaceId: string, request: WorkspaceUpdateRequest) => Promise<void>;
  onDeleteWorkspace: (workspaceId: string) => Promise<void>;
  onCreateAppInstance?: (
    workspaceId: string,
    request: WorkspaceAppInstanceCreateRequest
  ) => Promise<WorkspaceAppInstanceDto>;
}

export function OrganizationWorkspacesPage({
  loading,
  canManage,
  saving,
  deletingWorkspaceId,
  keyword,
  items,
  activeWorkspaceId,
  activeWorkspaceLabel,
  onKeywordChange,
  onOpenWorkspace,
  onCreateWorkspace,
  onUpdateWorkspace,
  onDeleteWorkspace
}: OrganizationWorkspacesPageProps) {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [createVisible, setCreateVisible] = useState(false);
  const [editTarget, setEditTarget] = useState<WorkspaceSummaryDto | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const createdWorkspaceId = searchParams.get("created");
  const [announcementTab, setAnnouncementTab] = useState<AnnouncementTab>("all");
  const [insightsLoading, setInsightsLoading] = useState(false);
  const [tutorials, setTutorials] = useState<TutorialCard[]>([]);
  const [announcements, setAnnouncements] = useState<AnnouncementItem[]>([]);
  const [recommended, setRecommended] = useState<RecommendedAgentItem[]>([]);
  const [recents, setRecents] = useState<RecentActivityItem[]>([]);

  const deferred = useDeferredValue(keyword);
  const filtered = useMemo(() => {
    if (!deferred.trim()) return items;
    const lower = deferred.trim().toLowerCase();
    return items.filter(
      item =>
        item.name?.toLowerCase().includes(lower) ||
        item.appKey?.toLowerCase().includes(lower) ||
        item.description?.toLowerCase().includes(lower)
    );
  }, [deferred, items]);

  // Support ?create=1 from workspace-switcher
  useEffect(() => {
    if (searchParams.get("create") === "1") {
      setCreateVisible(true);
      const next = new URLSearchParams(searchParams);
      next.delete("create");
      setSearchParams(next, { replace: true });
    }
  }, [searchParams, setSearchParams]);

  useEffect(() => {
    if (!activeWorkspaceId) {
      setTutorials([]);
      setAnnouncements([]);
      setRecommended([]);
      setRecents([]);
      return;
    }

    let cancelled = false;
    setInsightsLoading(true);

    Promise.all([
      getHomeTutorials(activeWorkspaceId),
      getHomeAnnouncements(activeWorkspaceId, { pageIndex: 1, pageSize: 10, tab: announcementTab }),
      getHomeRecommendedAgents(activeWorkspaceId),
      getHomeRecentActivities(activeWorkspaceId)
    ])
      .then(([tutorialResult, announcementResult, recommendedResult, recentResult]) => {
        if (cancelled) {
          return;
        }

        setTutorials(tutorialResult);
        setAnnouncements(announcementResult.items);
        setRecommended(recommendedResult);
        setRecents(recentResult);
      })
      .catch(() => {
        if (cancelled) {
          return;
        }

        setTutorials([]);
        setAnnouncements([]);
        setRecommended([]);
        setRecents([]);
      })
      .finally(() => {
        if (!cancelled) {
          setInsightsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [activeWorkspaceId, announcementTab]);

  const handleCreate = async (values: { name: string; description?: string }) => {
    try {
      await onCreateWorkspace(values);
      setCreateVisible(false);
      Toast.success(t("workspaceListCreatedSuccess"));
    } catch (err) {
      Toast.error((err as Error).message || t("workspaceListActionFailed"));
    }
  };

  const handleUpdate = async (values: { name: string; description?: string }) => {
    if (!editTarget) return;
    try {
      await onUpdateWorkspace(editTarget.id, values);
      setEditTarget(null);
      Toast.success(t("workspaceListUpdatedSuccess"));
    } catch (err) {
      Toast.error((err as Error).message || t("workspaceListActionFailed"));
    }
  };

  const handleDelete = async (workspaceId: string) => {
    setDeletingId(workspaceId);
    try {
      await onDeleteWorkspace(workspaceId);
      Toast.success(t("workspaceListArchivedSuccess"));
    } catch (err) {
      Toast.error((err as Error).message || t("workspaceListActionFailed"));
    } finally {
      setDeletingId(null);
    }
  };

  return (
    <div
      data-testid="workspace-list-page"
      style={{
        maxWidth: 1200,
        margin: "0 auto",
        padding: "20px 24px 24px",
        display: "flex",
        flexDirection: "column",
        gap: 16
      }}
    >
      {/* Page header */}
      <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", flexWrap: "wrap", gap: 12 }}>
        <div>
          <Title heading={3} style={{ margin: 0, color: "#1f2329", lineHeight: 1.2 }}>
          {t("workspaceListTitle")}
        </Title>
        <Text style={{ fontSize: 13, color: "#6b7280", marginTop: 4, display: "block", maxWidth: 760 }}>
          {t("workspaceListSubtitle")}
        </Text>        </div>
        <Button
          theme="solid"
          type="primary"
          icon={<IconPlus />}
          data-testid="workspace-create-btn"
          onClick={() => setCreateVisible(true)}
        >
          {t("workspaceListCreate")}
        </Button>
      </div>

      {/* Search bar */}
      <div style={{ maxWidth: 360 }}>
        <Input
          prefix={<IconSearch />}
          placeholder={t("workspaceListSearchPlaceholder")}
          value={keyword}
          onChange={onKeywordChange}
          showClear
          size="default"
          style={{ borderRadius: 12 }}
        />
      </div>

      {/* Grid */}
      {loading ? (
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))",
            gap: 12
          }}
        >
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton
              key={i}
              active
              placeholder={
                <div
                  style={{
                    background: "#fff",
                    border: "1px solid #f0f0f0",
                    borderRadius: 16,
                    padding: 20,
                    height: 160
                  }}
                >
                  <Skeleton.Avatar size="large" style={{ borderRadius: 10 }} />
                  <Skeleton.Title style={{ marginTop: 12 }} />
                  <Skeleton.Paragraph rows={1} style={{ marginTop: 8 }} />
                </div>
              }
            />
          ))}
        </div>
      ) : filtered.length === 0 ? (
        <div
          style={{
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            justifyContent: "center",
            padding: "80px 0",
            gap: 16,
            color: "#9ca3af"
          }}
        >
          <svg width="64" height="64" viewBox="0 0 64 64" fill="none" xmlns="http://www.w3.org/2000/svg">
            <rect x="8" y="16" width="48" height="36" rx="6" stroke="#d1d5db" strokeWidth="2.5" fill="none" />
            <rect x="16" y="8" width="32" height="12" rx="4" stroke="#d1d5db" strokeWidth="2" fill="none" />
            <line x1="20" y1="32" x2="44" y2="32" stroke="#d1d5db" strokeWidth="2.5" strokeLinecap="round" />
            <line x1="20" y1="40" x2="36" y2="40" stroke="#d1d5db" strokeWidth="2.5" strokeLinecap="round" />
          </svg>
          <div style={{ textAlign: "center" }}>
            <div style={{ fontSize: 16, fontWeight: 600, color: "#4b5563", marginBottom: 4 }}>
              {keyword ? t("workspaceListRecommendEmpty") : t("workspaceListEmpty")}
            </div>
            <div style={{ fontSize: 13, color: "#9ca3af" }}>
              {keyword ? t("workspaceListSearchPlaceholder") : t("workspaceListDescriptionFallback")}
            </div>
          </div>
          {!keyword && (
            <Button
              theme="solid"
              type="primary"
              icon={<IconPlus />}
              onClick={() => setCreateVisible(true)}
            >
              {t("workspaceListCreate")}
            </Button>
          )}
        </div>
      ) : (
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))",
            gap: 12
          }}
        >
          {filtered.map(item => (
            <WorkspaceCard
              key={item.id}
              item={item}
              deleting={(deletingWorkspaceId === item.id || deletingId === item.id)}
              onOpen={() => onOpenWorkspace(item.id)}
              onEdit={() => setEditTarget(item)}
              onDelete={() => {
                Modal.confirm({
                  title: t("workspaceListArchiveConfirmTitle"),
                  content: `${t("workspaceListArchiveConfirmTitle")} "${item.name || item.appKey}"？`,
                  okText: t("workspaceListArchive"),
                  cancelText: t("cancel"),
                  okType: "danger",
                  onOk: () => { void handleDelete(item.id); }
                });
              }}
            />
          ))}
        </div>
      )}

      <div className="coze-home-section" data-testid="workspace-list-insights" style={{ display: "flex", flexDirection: "column", gap: 12 }}>
        <header className="coze-home-section__head">
          <div>
            <Typography.Title heading={5} style={{ margin: 0 }}>
              {t("cozeHomeTutorialsTitle")}
            </Typography.Title>
            <Typography.Text type="tertiary">
              {(activeWorkspaceLabel || t("cozeShellWorkspaceSwitcherTitle")).trim()}
            </Typography.Text>
          </div>
        </header>

        {insightsLoading ? (
          <div className="coze-page__loading">
            <Spin />
          </div>
        ) : (
          <>
            <div className="coze-card-grid coze-card-grid--3" style={{ gap: 12 }}>
              {tutorials.map(card => (
                <button
                  key={card.id}
                  type="button"
                  className="coze-tutorial-card"
                  onClick={() => navigate(card.link)}
                  data-testid={`workspace-list-tutorial-${card.id}`}
                  style={{ minHeight: 112, padding: 14 }}
                >
                  <div className="coze-tutorial-card__icon" aria-hidden>
                    {card.iconKey === "intro" ? "?" : card.iconKey === "quickstart" ? ">" : "i"}
                  </div>
                  <strong>{card.title}</strong>
                  <span>{card.description}</span>
                </button>
              ))}
            </div>

            <section className="coze-home-row" style={{ alignItems: "stretch", gap: 12 }}>
              <div className="coze-home-row__main" style={{ minHeight: 220 }}>
                <header className="coze-home-section__head">
                  <Typography.Title heading={5} style={{ margin: 0 }}>
                    {t("cozeHomeAnnouncementsTitle")}
                  </Typography.Title>
                </header>
                <Tabs activeKey={announcementTab} onChange={key => setAnnouncementTab((key as AnnouncementTab) ?? "all")}>
                  <TabPane tab={t("cozeHomeAnnouncementsTabAll")} itemKey="all" />
                  <TabPane tab={t("cozeHomeAnnouncementsTabNotice")} itemKey="notice" />
                </Tabs>
                {announcements.length === 0 ? (
                  <Empty description={t("cozeHomeAnnouncementsEmpty")} />
                ) : (
                  <ul className="coze-list" style={{ maxHeight: 168, overflow: "auto" }}>
                    {announcements.map(item => (
                      <li key={item.id} className="coze-list__item">
                        <div>
                          <strong>{item.title}</strong>
                          <span>{item.summary}</span>
                        </div>
                        <div style={{ display: "flex", gap: 6, alignItems: "center" }}>
                          {item.tag ? <Tag size="small" color="blue">{item.tag}</Tag> : null}
                          <span style={{ color: "var(--semi-color-text-2)" }}>{item.publisher}</span>
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
              </div>

              <aside className="coze-home-row__aside" style={{ minHeight: 220 }}>
                <header className="coze-home-section__head">
                  <Typography.Title heading={5} style={{ margin: 0 }}>
                    {t("cozeHomeRecommendedTitle")}
                  </Typography.Title>
                </header>
                {recommended.length === 0 ? (
                  <Empty description={t("workspaceListRecommendEmpty")} />
                ) : (
                  <div className="coze-recommended-list" style={{ maxHeight: 168, overflow: "auto" }}>
                    {recommended.map(item => (
                      <div key={item.id} className="coze-recommended-item">
                        <Avatar size="small" color="light-blue">
                          {item.name.slice(0, 1)}
                        </Avatar>
                        <div className="coze-recommended-item__meta">
                          <strong>{item.name}</strong>
                          <span>{item.description}</span>
                        </div>
                        <Button
                          size="small"
                          theme="light"
                          type="primary"
                          onClick={() => navigate(item.link)}
                          data-testid={`workspace-list-recommended-${item.id}`}
                        >
                          {t("cozeHomeRecommendedTryNow")}
                        </Button>
                      </div>
                    ))}
                  </div>
                )}
              </aside>
            </section>

            <section className="coze-home-section">
              <header className="coze-home-section__head">
                <Typography.Title heading={5} style={{ margin: 0 }}>
                  {t("cozeHomeRecentTitle")}
                </Typography.Title>
              </header>
              {recents.length === 0 ? (
                <Empty description={t("cozeHomeRecentEmpty")} />
              ) : (
                <ul className="coze-list" style={{ maxHeight: 132, overflow: "auto" }}>
                  {recents.map(item => (
                    <li
                      key={item.id}
                      className="coze-list__item coze-list__item--clickable"
                      onClick={() => navigate(item.entryRoute)}
                      role="button"
                    >
                      <div>
                        <strong>{item.name}</strong>
                        <span>{item.description ?? ""}</span>
                      </div>
                      <Tag size="small" color="grey">{item.type}</Tag>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </>
        )}
      </div>

      {createdWorkspaceId ? (
        <div data-testid="workspace-created-marker" style={{ display: "none" }}>
          {createdWorkspaceId}
        </div>
      ) : null}

      {/* Create modal */}
      <WorkspaceFormModal
        visible={createVisible}
        saving={saving}
        title={t("workspaceListCreateDialogTitle")}
        okText={t("workspaceListCreate")}
        onOk={handleCreate}
        onCancel={() => setCreateVisible(false)}
      />

      {/* Edit modal */}
      <WorkspaceFormModal
        visible={editTarget !== null}
        saving={saving}
        title={t("workspaceListEditDialogTitle")}
        okText={t("workspaceListEdit")}
        initialValues={editTarget ? { name: editTarget.name, description: editTarget.description } : undefined}
        onOk={handleUpdate}
        onCancel={() => setEditTarget(null)}
      />
    </div>
  );
}
