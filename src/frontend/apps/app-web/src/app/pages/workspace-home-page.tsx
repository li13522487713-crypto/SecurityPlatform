import { useEffect, useMemo, useState } from "react";
import { Avatar, Button, Card, Empty, Space, Spin, TabPane, Tabs, Tag, Typography } from "@douyinfe/semi-ui";
import { useNavigate } from "react-router-dom";
import { useAppI18n } from "../i18n";
import { useWorkspaceContext } from "../workspace-context";
import {
  type AnnouncementItem,
  type AnnouncementTab,
  type HomeBanner,
  type RecentActivityItem,
  type RecommendedAgentItem,
  type TutorialCard,
  getHomeAnnouncements,
  getHomeBanner,
  getHomeRecentActivities,
  getHomeRecommendedAgents,
  getHomeTutorials
} from "../../services/api-home-content";
import { GlobalCreateModal } from "../components/global-create-modal";

export function WorkspaceHomePage() {
  const { t } = useAppI18n();
  const workspace = useWorkspaceContext();
  const navigate = useNavigate();
  const [banner, setBanner] = useState<HomeBanner | null>(null);
  const [tutorials, setTutorials] = useState<TutorialCard[]>([]);
  const [announcementTab, setAnnouncementTab] = useState<AnnouncementTab>("all");
  const [announcements, setAnnouncements] = useState<AnnouncementItem[]>([]);
  const [recommended, setRecommended] = useState<RecommendedAgentItem[]>([]);
  const [recents, setRecents] = useState<RecentActivityItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [createOpen, setCreateOpen] = useState(false);

  useEffect(() => {
    if (!workspace.id) {
      return;
    }
    let cancelled = false;
    setLoading(true);
    Promise.all([
      getHomeBanner(workspace.id),
      getHomeTutorials(workspace.id),
      getHomeAnnouncements(workspace.id, { pageIndex: 1, pageSize: 10, tab: announcementTab }),
      getHomeRecommendedAgents(workspace.id),
      getHomeRecentActivities(workspace.id)
    ])
      .then(([bannerResult, tutorialResult, announcementResult, recommendedResult, recentResult]) => {
        if (cancelled) {
          return;
        }
        setBanner(bannerResult);
        setTutorials(tutorialResult);
        setAnnouncements(announcementResult.items);
        setRecommended(recommendedResult);
        setRecents(recentResult);
      })
      .catch(() => {
        if (!cancelled) {
          setBanner(null);
          setTutorials([]);
          setAnnouncements([]);
          setRecommended([]);
          setRecents([]);
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
  }, [workspace.id, announcementTab]);

  const handleCta = (key: HomeBanner["ctaList"][number]["key"]) => {
    if (key === "create") {
      setCreateOpen(true);
    } else if (key === "tutorial") {
      navigate("/docs");
    } else if (key === "docs") {
      navigate("/docs");
    }
  };

  const workspaceContextHint = useMemo(
    () => t("cozeHomeWorkspaceContext").replace("{workspace}", workspace.name || workspace.appKey || ""),
    [t, workspace.appKey, workspace.name]
  );

  return (
    <div
      className="coze-page coze-home-page"
      data-testid="coze-home-page"
      style={{
        gap: 24,
        padding: "24px 32px"
      }}
    >
      {loading ? (
        <div className="coze-page__loading" style={{ padding: "80px 0" }}><Spin size="large" /></div>
      ) : (
        <>
          {banner ? (
            <Card
              style={{
                background: "linear-gradient(135deg, #f0f4ff 0%, #e0e7ff 100%)",
                borderRadius: 20,
                border: "1px solid rgba(37, 99, 235, 0.1)",
                boxShadow: "inset 0 2px 10px rgba(255, 255, 255, 0.6)"
              }}
              bodyStyle={{ padding: "40px 48px", display: "flex", gap: 24, alignItems: "center" }}
            >
              <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: 8 }}>
                <Typography.Text type="tertiary" style={{ fontWeight: 500 }}>{workspaceContextHint}</Typography.Text>
                <Typography.Title heading={2} style={{ margin: "4px 0 8px", lineHeight: 1.2, color: "#0f172a" }}>
                  {banner.heroTitle}
                </Typography.Title>
                <Typography.Paragraph style={{ marginBottom: 16, color: "#334155", fontSize: 15 }}>
                  {banner.heroSubtitle}
                </Typography.Paragraph>
                <Space spacing={12}>
                  {banner.ctaList.map(cta => (
                    <Button
                      key={cta.key}
                      theme={cta.key === "create" ? "solid" : "light"}
                      type={cta.key === "create" ? "primary" : "tertiary"}
                      onClick={() => handleCta(cta.key)}
                      data-testid={`coze-home-banner-cta-${cta.key}`}
                      size="large"
                      style={{ borderRadius: 8, fontWeight: 500 }}
                    >
                      {cta.label}
                    </Button>
                  ))}
                </Space>
              </div>
            </Card>
          ) : null}

          <section style={{ display: "flex", flexDirection: "column", gap: 16 }}>
            <Typography.Title heading={4} style={{ margin: 0 }}>{t("cozeHomeTutorialsTitle")}</Typography.Title>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(3, minmax(0, 1fr))", gap: 16 }}>
              {tutorials.map(card => (
                <Card
                  key={card.id}
                  shadows="hover"
                  style={{ borderRadius: 16, cursor: "pointer", border: "1px solid rgba(15, 23, 42, 0.06)" }}
                  bodyStyle={{ padding: 20 }}
                  data-testid={`coze-home-tutorial-${card.id}`}
                >
                  <div
                    role="button"
                    tabIndex={0}
                    onClick={() => navigate(card.link)}
                    onKeyDown={event => {
                      if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        navigate(card.link);
                      }
                    }}
                  >
                    <Space align="start" spacing={14}>
                      <div className="coze-tutorial-card__icon" aria-hidden>
                        {card.iconKey === "intro" ? "?" : card.iconKey === "quickstart" ? ">" : "i"}
                      </div>
                      <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                        <Typography.Text strong style={{ fontSize: 15, color: "#1d2129" }}>{card.title}</Typography.Text>
                        <Typography.Text type="tertiary" style={{ fontSize: 13 }}>{card.description}</Typography.Text>
                      </div>
                    </Space>
                  </div>
                </Card>
              ))}
            </div>
          </section>

          <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: 20 }}>
            <Card
              style={{ borderRadius: 16, border: "1px solid rgba(15, 23, 42, 0.06)" }}
              bodyStyle={{ padding: "20px 24px", display: "flex", flexDirection: "column", gap: 16, minHeight: 220 }}
            >
              <Typography.Title heading={4} style={{ margin: 0 }}>{t("cozeHomeAnnouncementsTitle")}</Typography.Title>
              <Tabs activeKey={announcementTab} onChange={key => setAnnouncementTab((key as AnnouncementTab) ?? "all")}>
                <TabPane tab={t("cozeHomeAnnouncementsTabAll")} itemKey="all" />
                <TabPane tab={t("cozeHomeAnnouncementsTabNotice")} itemKey="notice" />
              </Tabs>
              {announcements.length === 0 ? (
                <Empty description={t("cozeHomeAnnouncementsEmpty")} style={{ marginTop: 20 }} />
              ) : (
                <ul className="coze-list" style={{ maxHeight: 200, overflow: "auto" }}>
                  {announcements.map(item => (
                    <li key={item.id} className="coze-list__item" style={{ borderRadius: 10, padding: "14px 16px" }}>
                      <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                        <Typography.Text strong style={{ fontSize: 14 }}>{item.title}</Typography.Text>
                        <Typography.Text type="tertiary" style={{ fontSize: 13 }}>{item.summary}</Typography.Text>
                      </div>
                      <Space align="center" spacing={8}>
                        {item.tag ? <Tag size="small" color="blue" shape="circle">{item.tag}</Tag> : null}
                        <Typography.Text type="quaternary" style={{ fontSize: 12 }}>{item.publisher}</Typography.Text>
                      </Space>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            <Card
              style={{ borderRadius: 16, border: "1px solid rgba(15, 23, 42, 0.06)" }}
              bodyStyle={{ padding: "20px 24px", display: "flex", flexDirection: "column", gap: 16, minHeight: 220 }}
            >
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <Typography.Title heading={4} style={{ margin: 0 }}>{t("cozeHomeRecommendedTitle")}</Typography.Title>
                <Button theme="borderless" onClick={() => navigate("/explore/template")} size="small">
                  {t("cozeHomeRecommendedMore")}
                </Button>
              </div>
              <div className="coze-recommended-list" style={{ maxHeight: 200, overflow: "auto", display: "flex", flexDirection: "column", gap: 12 }}>
                {recommended.map(item => (
                  <div key={item.id} className="coze-recommended-item" style={{ padding: 10, borderRadius: 10, background: "#fafafa" }}>
                    <Space align="center" spacing={12} style={{ width: "100%" }}>
                      <Avatar size="small" color="light-blue" style={{ flexShrink: 0 }}>{item.name.slice(0, 1)}</Avatar>
                      <div style={{ flex: 1, display: "flex", flexDirection: "column", gap: 2, overflow: "hidden" }}>
                        <Typography.Text strong ellipsis style={{ fontSize: 14 }}>{item.name}</Typography.Text>
                        <Typography.Text type="tertiary" ellipsis style={{ fontSize: 12 }}>{item.description}</Typography.Text>
                      </div>
                      <Button
                        size="small"
                        theme="light"
                        type="primary"
                        onClick={() => navigate(item.link)}
                        data-testid={`coze-home-recommended-${item.id}`}
                        style={{ borderRadius: 6 }}
                      >
                        {t("cozeHomeRecommendedTryNow")}
                      </Button>
                    </Space>
                  </div>
                ))}
              </div>
            </Card>
          </div>

          <Card
            style={{ borderRadius: 16, border: "1px solid rgba(15, 23, 42, 0.06)" }}
            bodyStyle={{ padding: "20px 24px", display: "flex", flexDirection: "column", gap: 16 }}
          >
            <Typography.Title heading={4} style={{ margin: 0 }}>{t("cozeHomeRecentTitle")}</Typography.Title>
            {recents.length === 0 ? (
              <Empty description={t("cozeHomeRecentEmpty")} style={{ padding: "20px 0" }} />
            ) : (
              <ul className="coze-list" style={{ maxHeight: 200, overflow: "auto" }}>
                {recents.map(item => (
                  <li
                    key={item.id}
                    className="coze-list__item coze-list__item--clickable"
                    onClick={() => navigate(item.entryRoute)}
                    role="button"
                    style={{ borderRadius: 10, padding: "14px 16px" }}
                  >
                    <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                      <Typography.Text strong style={{ fontSize: 14 }}>{item.name}</Typography.Text>
                      <Typography.Text type="tertiary" style={{ fontSize: 13 }}>{item.description ?? ""}</Typography.Text>
                    </div>
                    <Tag size="small" color="grey" shape="circle">{item.type}</Tag>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </>
      )}

      <GlobalCreateModal
        visible={createOpen}
        workspaceId={workspace.id}
        onClose={() => setCreateOpen(false)}
      />
    </div>
  );
}
