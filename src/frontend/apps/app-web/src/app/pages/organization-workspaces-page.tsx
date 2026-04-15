import { useDeferredValue, useMemo } from "react";
import { Button, Empty, Input, Skeleton, Tag, Typography } from "@douyinfe/semi-ui";
import type { WorkspaceSummaryDto } from "@/services/api-org-workspaces";
import { useAppI18n } from "../i18n";

interface OrganizationWorkspacesPageProps {
  loading: boolean;
  keyword: string;
  items: WorkspaceSummaryDto[];
  onKeywordChange: (value: string) => void;
  onOpenWorkspace: (workspaceId: string) => void;
}

export function OrganizationWorkspacesPage({
  loading,
  keyword,
  items,
  onKeywordChange,
  onOpenWorkspace
}: OrganizationWorkspacesPageProps) {
  const { t } = useAppI18n();
  const deferredKeyword = useDeferredValue(keyword.trim().toLowerCase());

  const filteredItems = useMemo(() => {
    if (!deferredKeyword) {
      return items;
    }

    return items.filter(item => {
      const haystack = `${item.name} ${item.description ?? ""} ${item.appKey}`.toLowerCase();
      return haystack.includes(deferredKeyword);
    });
  }, [deferredKeyword, items]);

  return (
    <div className="atlas-workspaces-page" data-testid="workspace-list-page">
      <section className="atlas-workspaces-hero">
        <div className="atlas-workspaces-hero__copy">
          <span className="atlas-workspaces-hero__kicker">{t("workspaceListKicker")}</span>
          <Typography.Title heading={2} style={{ margin: 0 }}>
            {t("workspaceListTitle")}
          </Typography.Title>
          <Typography.Text type="tertiary">
            {t("workspaceListSubtitle")}
          </Typography.Text>
        </div>

        <div className="atlas-workspaces-hero__actions">
          <Input
            value={keyword}
            onChange={onKeywordChange}
            showClear
            placeholder={t("workspaceListSearchPlaceholder")}
            data-testid="workspace-list-search"
          />
        </div>
      </section>

      {loading ? (
        <div className="atlas-workspaces-grid">
          {Array.from({ length: 3 }).map((_, index) => (
            <article key={index} className="atlas-workspace-card atlas-workspace-card--loading">
              <Skeleton placeholder={<Skeleton.Title style={{ width: "56%" }} />} loading active />
              <Skeleton placeholder={<Skeleton.Paragraph rows={3} />} loading active />
            </article>
          ))}
        </div>
      ) : filteredItems.length === 0 ? (
        <div className="atlas-workspaces-empty">
          <Empty description={t("workspaceListEmpty")} />
        </div>
      ) : (
        <div className="atlas-workspaces-grid">
          {filteredItems.map(item => (
            <article key={item.id} className="atlas-workspace-card">
              <div className="atlas-workspace-card__head">
                <div>
                  <Tag color="blue">{t("workspaceListWorkspaceTag")}</Tag>
                  <Typography.Title heading={5} style={{ margin: "10px 0 0" }}>
                    {item.name}
                  </Typography.Title>
                </div>
                <Tag color="light-blue">{item.roleCode}</Tag>
              </div>

              <Typography.Text type="tertiary" className="atlas-workspace-card__description">
                {item.description || t("workspaceListDescriptionFallback")}
              </Typography.Text>

              <div className="atlas-workspace-card__meta">
                <span>{t("workspaceListAppCount")}: {item.appCount}</span>
                <span>{t("workspaceListAgentCount")}: {item.agentCount}</span>
                <span>{t("workspaceListWorkflowCount")}: {item.workflowCount}</span>
              </div>

              <div className="atlas-workspace-card__footer">
                <div className="atlas-workspace-card__caption">
                  <strong>{item.appKey}</strong>
                  <span>{item.lastVisitedAt ? t("workspaceListVisited") : t("workspaceListCreated")}</span>
                </div>
                <Button
                  type="primary"
                  theme="solid"
                  onClick={() => onOpenWorkspace(item.id)}
                  data-testid={`workspace-open-${item.id}`}
                >
                  {t("workspaceListOpen")}
                </Button>
              </div>
            </article>
          ))}
        </div>
      )}
    </div>
  );
}
