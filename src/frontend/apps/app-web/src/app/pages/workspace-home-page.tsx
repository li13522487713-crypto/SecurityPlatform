import { useEffect, useMemo, useState } from "react";
import { Avatar, Button, Empty, Spin, TabPane, Tabs, Tag, Typography } from "@douyinfe/semi-ui";
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
} from "../../services/mock";
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
    <div className="coze-page coze-home-page" data-testid="coze-home-page">
      {loading ? (
        <div className="coze-page__loading"><Spin /></div>
      ) : (
        <>
          {banner ? (
            <section className="coze-home-banner">
              <div className="coze-home-banner__copy">
                <Typography.Text type="tertiary">{workspaceContextHint}</Typography.Text>
                <Typography.Title heading={2} style={{ margin: "8px 0 12px" }}>{banner.heroTitle}</Typography.Title>
                <Typography.Paragraph>{banner.heroSubtitle}</Typography.Paragraph>
                <div className="coze-home-banner__cta">
                  {banner.ctaList.map(cta => (
                    <Button
                      key={cta.key}
                      theme={cta.key === "create" ? "solid" : "light"}
                      type={cta.key === "create" ? "primary" : "tertiary"}
                      onClick={() => handleCta(cta.key)}
                      data-testid={`coze-home-banner-cta-${cta.key}`}
                    >
                      {cta.label}
                    </Button>
                  ))}
                </div>
              </div>
            </section>
          ) : null}

          <section className="coze-home-section">
            <header className="coze-home-section__head">
              <Typography.Title heading={5} style={{ margin: 0 }}>{t("cozeHomeTutorialsTitle")}</Typography.Title>
            </header>
            <div className="coze-card-grid coze-card-grid--3">
              {tutorials.map(card => (
                <button
                  key={card.id}
                  type="button"
                  className="coze-tutorial-card"
                  onClick={() => navigate(card.link)}
                  data-testid={`coze-home-tutorial-${card.id}`}
                >
                  <div className="coze-tutorial-card__icon" aria-hidden>
                    {card.iconKey === "intro" ? "?" : card.iconKey === "quickstart" ? ">" : "i"}
                  </div>
                  <strong>{card.title}</strong>
                  <span>{card.description}</span>
                </button>
              ))}
            </div>
          </section>

          <section className="coze-home-row">
            <div className="coze-home-row__main">
              <header className="coze-home-section__head">
                <Typography.Title heading={5} style={{ margin: 0 }}>{t("cozeHomeAnnouncementsTitle")}</Typography.Title>
              </header>
              <Tabs activeKey={announcementTab} onChange={key => setAnnouncementTab((key as AnnouncementTab) ?? "all")}>
                <TabPane tab={t("cozeHomeAnnouncementsTabAll")} itemKey="all" />
                <TabPane tab={t("cozeHomeAnnouncementsTabNotice")} itemKey="notice" />
              </Tabs>
              {announcements.length === 0 ? (
                <Empty description={t("cozeHomeAnnouncementsEmpty")} />
              ) : (
                <ul className="coze-list">
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

            <aside className="coze-home-row__aside">
              <header className="coze-home-section__head">
                <Typography.Title heading={5} style={{ margin: 0 }}>{t("cozeHomeRecommendedTitle")}</Typography.Title>
                <Button theme="borderless" onClick={() => navigate("/explore/template")}>
                  {t("cozeHomeRecommendedMore")}
                </Button>
              </header>
              <div className="coze-recommended-list">
                {recommended.map(item => (
                  <div key={item.id} className="coze-recommended-item">
                    <Avatar size="small" color="light-blue">{item.name.slice(0, 1)}</Avatar>
                    <div className="coze-recommended-item__meta">
                      <strong>{item.name}</strong>
                      <span>{item.description}</span>
                    </div>
                    <Button
                      size="small"
                      theme="light"
                      type="primary"
                      onClick={() => navigate(item.link)}
                      data-testid={`coze-home-recommended-${item.id}`}
                    >
                      {t("cozeHomeRecommendedTryNow")}
                    </Button>
                  </div>
                ))}
              </div>
            </aside>
          </section>

          <section className="coze-home-section">
            <header className="coze-home-section__head">
              <Typography.Title heading={5} style={{ margin: 0 }}>{t("cozeHomeRecentTitle")}</Typography.Title>
            </header>
            {recents.length === 0 ? (
              <Empty description={t("cozeHomeRecentEmpty")} />
            ) : (
              <ul className="coze-list">
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

      <GlobalCreateModal
        visible={createOpen}
        workspaceId={workspace.id}
        onClose={() => setCreateOpen(false)}
      />
    </div>
  );
}
